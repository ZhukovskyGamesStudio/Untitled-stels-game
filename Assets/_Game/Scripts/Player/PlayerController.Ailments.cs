using Stealth.Game;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// What robots do to a player short of knocking them out (<see cref="AilmentRules"/>): a drone's net (hold E to tear
    /// out), a banshee's stun, the muddled head after the nurse's bed, the goo, smoke and gas left on the floor
    /// (<see cref="Hazards"/>). The server owns the net, the stun and the muddle; the owner reads the patches it stands in.
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>Server-owned: caught in a net — no walking until torn out of it.</summary>
        public bool Netted { get; private set; }
        /// <summary>Server-owned: stunned until this server time.</summary>
        public double StunnedUntil { get; private set; }
        /// <summary>Server-owned: the muddle and when it wears off (server time).</summary>
        public Muddle Muddled { get; private set; }
        public double MuddledUntil { get; private set; }

        public bool Stunned => Session.ServerTime < StunnedUntil;
        /// <summary>The muddle in effect now.</summary>
        public Muddle ActiveMuddle => Session.ServerTime < MuddledUntil ? Muddled : Muddle.None;
        /// <summary>Owner: 0..1 of tearing out of the net.</summary>
        public float StruggleShare => m_Ailments.NetHoldTime > 0f ? Mathf.Clamp01(m_Struggle / m_Ailments.NetHoldTime) : 0f;
        /// <summary>Owner: standing in the cleaner's goo / in smoke right now.</summary>
        public bool InGoo { get; private set; }
        public bool InSmoke { get; private set; }

        AilmentSettings m_Ailments = new();
        float m_Struggle, m_NettedAt;

        /// <summary>Client: the server's ailments of this player.</summary>
        public void ApplyAilments(bool netted, double stunnedUntil, Muddle muddle, double muddledUntil)
        {
            Netted = netted;
            StunnedUntil = stunnedUntil;
            Muddled = muddle;
            MuddledUntil = muddledUntil;
        }

        /// <summary>
        /// Owner: what the ailments do to this frame's input — a muddled head turns the keys round, a net or a stun holds
        /// the legs; holding E tears the net. Also reads the patches under the feet (goo slows, smoke blinds, gas is gas).
        /// </summary>
        PlayerInputState AilInput(PlayerInputState input)
        {
            double now = Session.ServerTime;
            Hazards.Expire(now);
            InGoo = !Downed && Hazards.At(HazardKind.Goo, transform.position, now);
            InSmoke = Hazards.At(HazardKind.Smoke, transform.position, now);
            var muddle = ActiveMuddle;
            input.Move = AilmentRules.Move(input.Move, muddle);
            input.Look = AilmentRules.Look(input.Look, muddle);
            if (Netted || Stunned)
            {
                input.Move = Vector2.zero;
                input.Jump = false;
                input.Sprint = false;
            }
            if (Netted && !Downed && !GameMenu.IsOpen && Controls.Held(GameAction.Interact))
            {
                m_Struggle += Time.deltaTime;
                if (m_Struggle >= m_Ailments.NetHoldTime)
                {
                    m_Struggle = 0f;
                    Link.SendStruggle();
                }
            }
            else if (!Netted) m_Struggle = 0f;
            return input;
        }

        /// <summary>Owner: share of the walking speed the ailments leave.</summary>
        float AilmentSpeed => AilmentRules.Speed(InGoo, Netted, Stunned, m_Ailments);

        // ------------------------------------------------------------------ server

        /// <summary>Server: a net lands on the player.</summary>
        public void NetServer()
        {
            if (!IsServer || Downed || Invulnerable) return;
            Netted = true;
            m_NettedAt = Time.time;
        }

        /// <summary>Server: the owner tore out of the net.</summary>
        public void OnStruggleRequest() => Netted = false;

        /// <summary>Server: stunned for so many seconds (a banshee's shriek).</summary>
        public void StunServer(float seconds)
        {
            if (!IsServer || Downed || Invulnerable) return;
            StunnedUntil = System.Math.Max(StunnedUntil, Session.ServerTime + seconds);
        }

        /// <summary>Server: a muddled head for a while (which one — by the seeded number).</summary>
        public void MuddleServer(int seed)
        {
            if (!IsServer) return;
            Muddled = AilmentRules.Pick(seed);
            MuddledUntil = Session.ServerTime + m_Ailments.MuddleTime;
        }

        /// <summary>Server, every frame: a net falls off by itself in the end, and off a player who went down.</summary>
        void UpdateAilmentsServer()
        {
            if (Netted && (Downed || Time.time - m_NettedAt >= m_Ailments.NetTimeout)) Netted = false;
        }

        void ClearAilmentsServer()
        {
            Netted = false;
            StunnedUntil = 0;
            MuddledUntil = 0;
            Muddled = Muddle.None;
        }
    }
}
