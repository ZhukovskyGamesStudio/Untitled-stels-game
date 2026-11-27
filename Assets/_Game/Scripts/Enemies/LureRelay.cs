using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// The relay: a static loudspeaker in a trapped room. Every so often, when players are about but not in its room, it
    /// calls in a teammate's voice — the team sees a "help!" ping at it (<see cref="GameManager.LureCallServer"/>) and
    /// hears the call. It senses nobody itself; the traps round it do the rest. Hacked, it is silent.
    /// </summary>
    public class LureRelay : SecurityDevice
    {
        public RelayProfile Profile;

        public override DetectorProfile ProfileBase => Profile;
        protected override DeviceProfile Device => Profile;
        public override bool GlowEyes => false;
        public override float StateLine => 0.35f;

        const byte PulseLure = 14;
        float m_NextCall;
        System.Random m_Rng;

        protected override void OnDeviceSpawned()
        {
            if (!IsServer) return;
            m_Rng = LevelRandom.Next(RandomChannel.Devices);
            m_NextCall = Time.time + Profile.Period * m_Rng.Range(0.5f, 1f);
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
            GameManager.Instance?.LureCallServer(transform.position, m_Rng.Next());
            Pulse(PulseLure);
        }

        protected override void OnPulse(byte effect)
        {
            if (effect == PulseLure) Audio.Sfx.At(Audio.Sfx.Bank?.LureCall, Position, 1f, Audio.Sfx.Group.Robots, 0.9f + (NetId % 4) * 0.07f);
        }
    }
}
