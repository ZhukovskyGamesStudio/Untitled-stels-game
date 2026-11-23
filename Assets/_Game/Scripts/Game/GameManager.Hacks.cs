using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Level;

namespace Stealth.Game
{
    /// <summary>
    /// The robots and devices the team keeps switched off (a hack by hand or from a terminal): off for good, at most
    /// <see cref="Player.InteractionSettings.MaxHacked"/> at once — one more wakes the first. How many slots are in use
    /// is replicated as keys of <see cref="IsOpened"/> (<see cref="HackSlotKey"/>), the HUD shows them.
    /// </summary>
    public partial class GameManager
    {

        readonly List<Detector> m_Hacked = new();

        /// <summary>How many of the team's hack slots are in use (every peer).</summary>
        public int HackedCount
        {
            get
            {
                int n = 0;
                while (HackedKind(n) >= 0) n++;
                return n;
            }
        }

        /// <summary>The kind (<see cref="RobotKind"/>) in the team's <paramref name="slot"/>-th hack slot, oldest first — the next to wake — or -1 (every peer).</summary>
        public int HackedKind(int slot)
        {
            for (int k = 0; k < RobotRoster.KindCount; k++)
                if (IsOpened(OpenKeys.HackSlot(slot, k))) return k;
            return -1;
        }

        /// <summary>The team's hack slots (every peer): one more while anyone carries an upgraded keyboard.</summary>
        public int MaxHacked => CraftedHackSlots();

        /// <summary>Server: the team switches a robot / device off (for good, within the limit: over it the first one wakes).</summary>
        public void TeamHackServer(Detector target)
        {
            if (target == null || m_Unscrewed.Contains(target)) return; // (taken apart: off for good already)
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
            m_Unscrewed.RemoveWhere(d => d == null);
            // the upgraded keyboard left the team (dropped, burned): the slot it gave goes, its robot wakes
            bool woke = false;
            for (int max = MaxHacked; m_Hacked.Count > max; woke = true)
            {
                var first = m_Hacked[0];
                m_Hacked.RemoveAt(0);
                first.WakeServer();
            }
            if (m_Hacked.Count != before || woke) SyncHackSlots();
        }

        void SyncHackSlots()
        {
            bool changed = false;
            for (int i = 0; i < 8; i++)
                for (int k = 0; k < RobotRoster.KindCount; k++)
                {
                    bool on = i < m_Hacked.Count && (int)m_Hacked[i].Kind == k, was = m_Opened.Contains(OpenKeys.HackSlot(i, k));
                    if (on == was) continue;
                    if (on) m_Opened.Add(OpenKeys.HackSlot(i, k)); else m_Opened.Remove(OpenKeys.HackSlot(i, k));
                    changed = true;
                }
            if (changed) OpenedVersion++;
        }
    }
}
