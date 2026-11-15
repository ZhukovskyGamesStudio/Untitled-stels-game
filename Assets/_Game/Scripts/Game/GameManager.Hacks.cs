using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;

namespace Stealth.Game
{
    /// <summary>
    /// The robots and devices the team keeps switched off (a hack by hand or from a terminal): off for good, at most
    /// <see cref="Player.InteractionSettings.MaxHacked"/> at once — one more wakes the first. How many slots are in use
    /// is replicated as keys of <see cref="IsOpened"/> (<see cref="HackSlotKey"/>), the HUD shows them.
    /// </summary>
    public partial class GameManager
    {
        public static int HackSlotKey(int slot) => 9_100_000 + slot;

        readonly List<Detector> m_Hacked = new();

        /// <summary>How many of the team's hack slots are in use (every peer).</summary>
        public int HackedCount
        {
            get
            {
                int n = 0;
                while (IsOpened(HackSlotKey(n))) n++;
                return n;
            }
        }

        public int MaxHacked => System.Math.Max(1, Tuning.Player.Interaction.MaxHacked);

        /// <summary>Server: the team switches a robot / device off (for good, within the limit: over it the first one wakes).</summary>
        public void TeamHackServer(Detector target)
        {
            if (target == null) return;
            m_Hacked.RemoveAll(d => d == null || !d.IsHacked || d == target);
            while (m_Hacked.Count >= MaxHacked)
            {
                var first = m_Hacked[0];
                m_Hacked.RemoveAt(0);
                first.WakeServer();
            }
            target.HackServer(Tuning.Player.Interaction.HackDuration);
            m_Hacked.Add(target);
            SyncHackSlots();
        }

        /// <summary>Server, every frame: a hacked one that woke up (a timed hack ran out, it was despawned) frees its slot.</summary>
        void UpdateHacks()
        {
            int before = m_Hacked.Count;
            m_Hacked.RemoveAll(d => d == null || !d.IsHacked);
            if (m_Hacked.Count != before) SyncHackSlots();
        }

        void SyncHackSlots()
        {
            bool changed = false;
            for (int i = 0; i < 8; i++)
            {
                bool on = i < m_Hacked.Count, was = m_Opened.Contains(HackSlotKey(i));
                if (on == was) continue;
                if (on) m_Opened.Add(HackSlotKey(i)); else m_Opened.Remove(HackSlotKey(i));
                changed = true;
            }
            if (changed) OpenedVersion++;
        }
    }
}
