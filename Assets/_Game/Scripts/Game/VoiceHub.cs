using System;
using System.Collections.Generic;
using Stealth.Audio;
using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>How voice frames travel (Stealth.Net: unreliable named messages). Null = no session: nothing leaves this peer.</summary>
    public interface IVoiceTransport
    {
        /// <summary>Owner → server: one frame of this peer's microphone.</summary>
        void ToServer(byte[] frame, int length);
        /// <summary>Server → every remote peer except <paramref name="except"/> (the speaker): the relayed frame.</summary>
        void ToClients(byte[] relay, int length, ulong except);
        /// <summary>Server → every remote peer, reliably: a relay plays a teammate's recorded words (<see cref="VoicePacket.WriteLure"/>).</summary>
        void LureToClients(byte[] message, int length);
    }

    /// <summary>
    /// The voice chat's switchboard. A microphone frame goes to the server (<see cref="SendLocal"/>); the server makes
    /// the speaker's noise for the robots (<see cref="NoiseSystem"/>: the same hearing as footsteps, through walls and
    /// doors) and relays the frame to everyone else; each peer hands it to the speaker's <see cref="VoicePlayback"/>,
    /// which plays it from the speaker's mouth.
    /// </summary>
    public static class VoiceHub
    {
        public static IVoiceTransport Transport { get; set; }

        static readonly byte[] s_Relay = new byte[VoicePacket.MaxSize];
        static readonly Dictionary<ulong, float> s_NextNoise = new();
        static readonly Dictionary<ulong, float> s_Loudest = new();
        /// <summary>Last time each speaker's frame arrived (HUD: who is talking).</summary>
        static readonly Dictionary<ulong, float> s_Heard = new();
        /// <summary>
        /// Server: the last words of each player in this house, for the relays (<see cref="LureServer"/>). Recorded only
        /// while the house has a relay (<see cref="RememberServer"/>), only what the team was sent, only in memory.
        /// </summary>
        static readonly VoiceMemory s_Memory = new();
        static readonly List<ArraySegment<byte>> s_Snippet = new();
        static readonly List<ArraySegment<byte>> s_LureIn = new();
        static int s_KeepFrames;
        static float s_ForgetAfter = 60f;
        /// <summary>A relay's call carries at most this many frames (the count is two bytes; 10 s is plenty).</summary>
        const int MaxLureFrames = 500;

        static VoiceSettings Settings => Tuning.Audio.Voice;

        /// <summary>This peer's microphone produced a frame.</summary>
        public static void SendLocal(byte[] frame, int length)
        {
            ulong me = Session.LocalClientId;
            if (Session.IsServer) ReceiveServer(me, frame, 0, length);
            else Transport?.ToServer(frame, length);
        }

        /// <summary>Server: a frame from <paramref name="speaker"/> (a remote client, or the host itself).</summary>
        public static void ReceiveServer(ulong speaker, byte[] buffer, int offset, int length)
        {
            if (!VoicePacket.Read(buffer, offset, length, out ushort seq, out float level, out _, out _)) return;
            var player = PlayerController.Find(speaker);
            if (player == null) return;
            MakeNoise(player, level);
            if (s_KeepFrames > 0) s_Memory.Add(speaker, seq, level >= Settings.WhisperDb, Time.time, buffer, offset, length, s_KeepFrames);
            if (Transport != null)
            {
                int n = VoicePacket.WriteRelay(s_Relay, speaker, buffer, offset, length);
                Transport.ToClients(s_Relay, n, speaker);
            }
            if (speaker != Session.LocalClientId) Deliver(speaker, buffer, offset, length); // the host hears it too
        }

        /// <summary>Client: a relayed frame from the server.</summary>
        public static void ReceiveRelay(byte[] buffer, int offset, int length)
        {
            if (length <= 8) return;
            ulong speaker = VoicePacket.ReadSpeaker(buffer, offset);
            if (speaker == Session.LocalClientId) return;
            Deliver(speaker, buffer, offset + 8, length - 8);
        }

        /// <summary>Seconds since a frame of this speaker arrived (∞ if never).</summary>
        public static float SilentFor(ulong speaker) =>
            s_Heard.TryGetValue(speaker, out float t) ? Time.unscaledTime - t : float.PositiveInfinity;

        static void Deliver(ulong speaker, byte[] buffer, int offset, int length)
        {
            if (!VoicePacket.Read(buffer, offset, length, out ushort seq, out _, out int payload, out int size)) return;
            s_Heard[speaker] = Time.unscaledTime;
            var player = PlayerController.Find(speaker);
            if (player == null || player.Voice == null) return;
            player.Voice.Push(seq, buffer, payload, size);
        }

        /// <summary>
        /// The robots hear a talking player like a step of the same loudness: the loudest frame of every
        /// <see cref="VoiceSettings.NoiseEvery"/> becomes one noise at the mouth. Knocked down and invisible players make none.
        /// </summary>
        static void MakeNoise(PlayerController player, float levelDb)
        {
            ulong id = player.ClientId;
            var s = Settings;
            float radius = VoiceNoise.Radius(levelDb, s);
            s_Loudest[id] = Mathf.Max(radius, s_Loudest.TryGetValue(id, out float r) ? r : 0f);
            float now = Time.time;
            if (s_NextNoise.TryGetValue(id, out float next) && now < next) return;
            s_NextNoise[id] = now + s.NoiseEvery;
            float loudest = s_Loudest[id];
            s_Loudest[id] = 0f;
            if (loudest <= 0f || player.Downed || player.Invisible) return;
            NoiseSystem.EmitVoiceServer(player.transform.position, loudest, id);
            GameManager.Instance?.NoteServer(player, StatKind.Talk, s.NoiseEvery);
            // every radio in a teammate's hand sounds with the voice: the robots near it hear it, wherever the speaker is
            float radio = Tuning.Items.Gadgets.RadioNoise;
            if (radio <= 0f) return;
            foreach (var listener in PlayerController.All)
                if (listener != player && !listener.Invisible && listener.HoldsRadio)
                    NoiseSystem.EmitVoiceServer(listener.transform.position, radio, listener.ClientId);
        }

        /// <summary>A session ended: forget the speakers.</summary>
        public static void Clear()
        {
            s_NextNoise.Clear();
            s_Loudest.Clear();
            s_Heard.Clear();
            s_Memory.Clear();
        }

        // ------------------------------------------------------------------ the relay plays the players' own words

        /// <summary>Server: a relay stands in this house — remember the last <paramref name="seconds"/> of each player's speech.</summary>
        public static void RememberServer(float seconds, float forgetAfter)
        {
            s_KeepFrames = Mathf.Max(s_KeepFrames, Mathf.CeilToInt(seconds / VoiceMemory.FrameSeconds));
            s_ForgetAfter = forgetAfter;
        }

        /// <summary>Server: a house starts — forget every word, record nothing until one of its relays asks.</summary>
        public static void ForgetSpeechServer()
        {
            s_Memory.Clear();
            s_KeepFrames = 0;
        }

        /// <summary>
        /// Server: relay <paramref name="relay"/> calls in the voice of one of <paramref name="candidates"/> — picks a
        /// stretch of their recent speech (<see cref="VoiceMemory.Pick"/>) and sends it to every peer, which plays it from
        /// the relay (<see cref="ReceiveLure"/>). False: none of them has said enough lately.
        /// </summary>
        public static bool LureServer(ulong relay, IReadOnlyList<ulong> candidates, int roll, float minSeconds, float maxSeconds, out ulong speaker)
        {
            int min = Mathf.CeilToInt(minSeconds / VoiceMemory.FrameSeconds);
            int max = Mathf.Min(MaxLureFrames, Mathf.FloorToInt(maxSeconds / VoiceMemory.FrameSeconds));
            if (!s_Memory.Pick(candidates, roll, min, max, Time.time, s_ForgetAfter, s_Snippet, out speaker)) return false;
            var message = new byte[VoicePacket.LureSize(s_Snippet)];
            int n = VoicePacket.WriteLure(message, relay, s_Snippet);
            s_Snippet.Clear();
            Transport?.LureToClients(message, n);
            ReceiveLure(message, 0, n); // the host hears it too
            return true;
        }

        /// <summary>Every peer: the server's relay call — the relay plays the recorded words.</summary>
        public static void ReceiveLure(byte[] buffer, int offset, int length)
        {
            if (!VoicePacket.ReadLure(buffer, offset, length, out ulong relay, s_LureIn)) return;
            foreach (var d in Stealth.Enemies.Detector.All)
                if (d.NetId == relay && d is Stealth.Enemies.LureRelay r)
                {
                    r.PlayVoice(s_LureIn);
                    break;
                }
            s_LureIn.Clear();
        }
    }
}
