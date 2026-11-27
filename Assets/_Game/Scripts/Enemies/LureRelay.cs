using System;
using System.Collections.Generic;
using Stealth.Audio;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// The relay: a static loudspeaker in a trapped room. Every so often, when players are about but not in its room, it
    /// calls in a teammate's voice — the team sees a "help!" ping at it (<see cref="GameManager.LureCallServer"/>) and
    /// hears the call: a couple of seconds of what that teammate really said in this house, recorded by the server from
    /// the voice chat (<see cref="VoiceHub.LureServer"/>), played here in their voice; the synthetic call when nobody far
    /// from it has spoken. It senses nobody itself; the traps round it do the rest. Hacked, it is silent.
    /// </summary>
    public class LureRelay : SecurityDevice
    {
        public RelayProfile Profile;
        [Tooltip("Plays a teammate's recorded words from the loudspeaker, in 3D, muffled by the walls like any voice.")]
        public VoicePlayback Voice;

        public override DetectorProfile ProfileBase => Profile;
        protected override DeviceProfile Device => Profile;
        public override bool GlowEyes => false;
        public override float StateLine => 0.35f;

        const byte PulseLure = 14;
        /// <summary>Frames handed to the voice's jitter buffer ahead of their time (it waits for a cushion of a few first).</summary>
        const int WordsAhead = 6;
        float m_NextCall;
        System.Random m_Rng;
        readonly List<ulong> m_Far = new();
        // the recorded words being played (every peer): Opus payloads fed to Voice at the voice's own 20 ms pace
        readonly Queue<byte[]> m_Words = new();
        float m_WordsClock;
        int m_WordsPushed;
        ushort m_Seq;

        protected override void OnDeviceSpawned()
        {
            if (!IsServer) return;
            m_Rng = LevelRandom.Next(RandomChannel.Devices);
            m_NextCall = Time.time + Profile.Period * m_Rng.Range(0.5f, 1f);
            VoiceHub.RememberServer(Profile.RememberSeconds, Profile.ForgetAfter);
        }

        /// <summary>It calls now: someone is in earshot, nobody close enough to see who is calling (pure).</summary>
        public static bool Calls(float nearest, float earshot, float tooClose) => nearest <= earshot && nearest > tooClose;

        protected override void TickServer()
        {
            if (Time.time < m_NextCall) return;
            m_NextCall = Time.time + Profile.Period * m_Rng.Range(0.8f, 1.25f);
            float nearest = float.MaxValue;
            foreach (var p in PlayerController.All)
                if (!((IIntruder)p).IsDown) nearest = Mathf.Min(nearest, Vector3.Distance(p.transform.position, transform.position));
            if (!Calls(nearest, Profile.Earshot, Profile.TooClose)) return;
            int roll = m_Rng.Next();
            var game = GameManager.Instance;
            // the voice of a player far from it, as the ping's name (the one player left would know it was not them)
            m_Far.Clear();
            foreach (var p in PlayerController.All)
                if (!p.Downed && Vector3.Distance(p.transform.position, transform.position) > GameManager.LureNear) m_Far.Add(p.ClientId);
            ulong speaker = 0;
            bool voiced = game != null && game.Phase == RunPhase.Playing && m_Far.Count >= 2
                && VoiceHub.LureServer(NetId, m_Far, roll, Profile.SnippetMin, Profile.SnippetMax, out speaker);
            if (game != null) game.LureCallServer(transform.position, roll, voiced ? speaker : (ulong?)null);
            if (!voiced) Pulse(PulseLure);
        }

        protected override void OnPulse(byte effect)
        {
            if (effect == PulseLure) Audio.Sfx.At(Audio.Sfx.Bank?.LureCall, Position, 1f, Audio.Sfx.Group.Robots, 0.9f + (NetId % 4) * 0.07f);
        }

        /// <summary>Every peer: the server sent a teammate's recorded words (voice frames as they came) to play from here.</summary>
        public void PlayVoice(IReadOnlyList<ArraySegment<byte>> frames)
        {
            if (Voice == null || !Voice.isActiveAndEnabled)
            {
                OnPulse(PulseLure); // no voice chat on this peer: the synthetic call
                return;
            }
            m_Words.Clear();
            m_WordsClock = 0f;
            m_WordsPushed = 0;
            m_Seq += 100; // a fresh run for the jitter buffer, well past the last one
            foreach (var f in frames)
            {
                if (!VoicePacket.Read(f.Array, f.Offset, f.Count, out _, out _, out int payload, out int size)) continue;
                var words = new byte[size];
                Buffer.BlockCopy(f.Array, payload, words, 0, size);
                m_Words.Enqueue(words);
            }
        }

        protected override void TickLocal()
        {
            if (m_Words.Count == 0 || Voice == null) return;
            m_WordsClock += Time.unscaledDeltaTime;
            int due = Mathf.FloorToInt(m_WordsClock / VoiceMemory.FrameSeconds) + WordsAhead;
            while (m_WordsPushed < due && m_Words.Count > 0)
            {
                var words = m_Words.Dequeue();
                Voice.Push(m_Seq++, words, 0, words.Length);
                m_WordsPushed++;
            }
        }
    }
}
