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
            if (!VoicePacket.Read(buffer, offset, length, out _, out float level, out _, out _)) return;
            var player = PlayerController.Find(speaker);
            if (player == null) return;
            MakeNoise(player, level);
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
        }
    }
}
