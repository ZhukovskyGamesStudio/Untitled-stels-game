using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>The player: safes (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        // ------------------------------------------------------------------ safes

        /// <summary>Owner: the dial being turned (null when not cracking a safe).</summary>
        public Items.SafeLock Cracking { get; private set; }
        public Items.SafeBox CrackingSafe { get; private set; }
        /// <summary>The vault's lock panel whose dial is being turned (the dial lock is the safe's minigame), or null.</summary>
        public VaultConsole CrackingConsole { get; private set; }
        /// <summary>Owner: a closed safe in reach (press E to start), null if none.</summary>
        public Items.SafeBox SafeTarget { get; private set; }
        /// <summary>Owner: the last try on the dial and when (for the minigame's feedback).</summary>
        public Items.SafeLock.Result LastTry { get; private set; }
        public float LastTryTime { get; private set; } = -10f;
        int m_CrackStartFrame;
        /// <summary>
        /// The dials this player has worked on in this house: walking away from a safe keeps the digits already found
        /// (two found, a walk, the third next time). Safes are new objects in every house, so stale keys just drop out.
        /// </summary>
        readonly Dictionary<Items.SafeBox, Items.SafeLock> m_Dials = new();

        /// <summary>The dial of this safe as it was left, or a new one (its code from the safe's seed).</summary>
        Items.SafeLock DialOf(Items.SafeBox safe)
        {
            if (m_Dials.TryGetValue(safe, out var dial)) return dial;
            var stale = new List<Items.SafeBox>();
            foreach (var k in m_Dials.Keys) if (k == null) stale.Add(k);
            foreach (var k in stale) m_Dials.Remove(k);
            var settings = Tuning.Items.Safes;
            dial = new Items.SafeLock(safe.Seed, settings);
            dial.SetDial(new System.Random(safe.Seed ^ Time.frameCount).Next(settings.DialSize));
            m_Dials[safe] = dial;
            return dial;
        }

        /// <summary>Test hook: start cracking this safe as if E was pressed next to it.</summary>
        public void StartCrackForTest(Items.SafeBox safe)
        {
            if (safe == null) { StopCracking(); return; }
            Cracking = DialOf(safe);
            CrackingSafe = safe;
            m_CrackStartFrame = Time.frameCount;
        }

        void UpdateSafe()
        {
            SafeTarget = ReviveTarget == null && HackTarget == null && DoorTarget == null ? Items.SafeBox.ClosedNear(transform.position, Hands, Items.SafeBox.Reach, View) : null;
            if (Cracking == null)
            {
                if (SafeTarget == null || !Controls.Pressed(GameAction.Interact) || CursorMode) return;
                Cracking = DialOf(SafeTarget); // the digits found before are still found
                CrackingSafe = SafeTarget;
                m_CrackStartFrame = Time.frameCount;
                LastTry = Items.SafeLock.Result.Nothing;
                return;
            }
            var safe = CrackingSafe;
            var host = safe != null ? safe.transform : CrackingConsole != null ? CrackingConsole.transform : null;
            bool away = host == null || (safe != null && safe.Opened) || (CrackingConsole != null && CrackingConsole.Solved)
                        || (host.position - transform.position).magnitude > Items.SafeBox.Reach + 0.6f;
            if (away || Controls.Pressed(GameAction.Jump) || Controls.Pressed(GameAction.Back))
            {
                StopCracking();
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                int ticks = Cracking.Turn(mouse.delta.ReadValue().x);
                if (ticks > 0)
                    Audio.Sfx.At(Audio.Sfx.Bank?.Click, host.position + Vector3.up, 0.15f + 0.85f * Cracking.Closeness(), Audio.Sfx.Group.Items,
                        0.8f + 0.5f * Cracking.Closeness());
            }
            bool tryNow = Time.frameCount != m_CrackStartFrame &&
                          (Controls.Pressed(GameAction.Interact) || (mouse != null && mouse.leftButton.wasPressedThisFrame));
            if (tryNow) TryDial();
        }

        /// <summary>Owner: try the number under the pointer (LMB / E; also a test hook).</summary>
        public void TryDial()
        {
            var safe = CrackingSafe;
            var host = safe != null ? safe.transform : CrackingConsole != null ? CrackingConsole.transform : null;
            if (Cracking == null || host == null) return;
            LastTry = Cracking.Try();
            LastTryTime = Time.time;
            switch (LastTry)
            {
                case Items.SafeLock.Result.Wrong:
                    // A loud clank: robots with ears may come.
                    Audio.Sfx.At(Audio.Sfx.Bank?.Drop, host.position + Vector3.up, 1f, Audio.Sfx.Group.Items, 0.6f);
                    EmitNoise(Tuning.Items.Safes.WrongNoise);
                    break;
                case Items.SafeLock.Result.Digit:
                    Audio.Sfx.At(Audio.Sfx.Bank?.Pickup, host.position + Vector3.up, 0.8f, Audio.Sfx.Group.Items, 1.3f);
                    break;
                case Items.SafeLock.Result.Opened:
                    Audio.Sfx.At(Audio.Sfx.Bank?.Stash, host.position + Vector3.up, 1f, Audio.Sfx.Group.Items);
                    if (safe != null)
                    {
                        Link.SendOpenSafe(safe.Index);
                    }
                    else SolveConsole(CrackingConsole);
                    StopCracking();
                    break;
            }
        }

        /// <summary>Owner: the dial (a safe's or a lock panel's) leaves the screen.</summary>
        void StopCracking()
        {
            Cracking = null;
            CrackingSafe = null;
            CrackingConsole = null;
        }

        /// <summary>Owner: out of every mode the hands can be busy in — a dial, a lock panel, a terminal (moved, unstuck, knocked out).</summary>
        void LeaveAllModes()
        {
            StopCracking();
            LeaveConsole();
            StopTerminal();
        }
    }
}
