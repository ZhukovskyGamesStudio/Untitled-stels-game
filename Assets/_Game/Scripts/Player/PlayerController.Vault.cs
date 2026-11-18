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
    /// <summary>The player: the vault's lock panels (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        // ------------------------------------------------------------------ the vault's lock panels

        /// <summary>The vault's lock panel whose switch or sequence lock is on the screen, or null (the dial: <see cref="CrackingConsole"/>).</summary>
        public VaultConsole AtConsole { get; private set; }
        /// <summary>The switch lock on the screen (<see cref="VaultGame.Switches"/>), or null.</summary>
        public Items.SwitchPuzzle Switches { get; private set; }
        /// <summary>The sequence lock on the screen (<see cref="VaultGame.Sequence"/>), or null.</summary>
        public Items.SequenceLock Sequence { get; private set; }
        /// <summary>When a lock last clanked on a wrong press (the screen flashes red).</summary>
        public float ConsoleWrongTime { get; private set; } = -10f;
        /// <summary>Busy at any lock of the vault (no walking, no reticle).</summary>
        public bool AtVaultLock => AtConsole != null || CrackingConsole != null;
        readonly Dictionary<VaultConsole, Items.SafeLock> m_ConsoleDials = new();
        readonly Dictionary<VaultConsole, Items.SwitchPuzzle> m_ConsoleSwitches = new();

        int ConsoleSeed(VaultConsole c) => LevelRandom.Mix(ActiveLevel.Layout != null ? ActiveLevel.Layout.Seed : 0, Salt.VaultConsoleCode, c.Index);

        static void ForgetGone<T>(Dictionary<VaultConsole, T> games)
        {
            if (games.Count == 0) return;
            var gone = new List<VaultConsole>();
            foreach (var c in games.Keys) if (c == null) gone.Add(c);
            foreach (var c in gone) games.Remove(c);
        }

        /// <summary>Owner: E at a lock panel puts its minigame on the screen (a dial or the switches keep their state while away).</summary>
        void StartConsole(VaultConsole c)
        {
            if (c == null || c.Solved) return;
            // the panels of houses left behind are gone: their minigames with them
            ForgetGone(m_ConsoleDials);
            ForgetGone(m_ConsoleSwitches);
            switch (c.Game)
            {
                case VaultGame.Dial:
                    if (!m_ConsoleDials.TryGetValue(c, out var dial))
                    {
                        var safes = Tuning.Items.Objectives.VaultDial(Tuning.Items.Safes);
                        dial = new Items.SafeLock(ConsoleSeed(c), safes);
                        dial.SetDial(new System.Random(ConsoleSeed(c) ^ 0x5A).Next(safes.DialSize));
                        m_ConsoleDials[c] = dial;
                    }
                    Cracking = dial;
                    CrackingConsole = c;
                    m_CrackStartFrame = Time.frameCount;
                    LastTry = Items.SafeLock.Result.Nothing;
                    break;
                case VaultGame.Switches:
                    if (!m_ConsoleSwitches.TryGetValue(c, out var switches)) m_ConsoleSwitches[c] = switches = new Items.SwitchPuzzle(ConsoleSeed(c), Tuning.Items.Objectives);
                    Switches = switches;
                    AtConsole = c;
                    break;
                default:
                    Sequence = new Items.SequenceLock(ConsoleSeed(c), Time.time, Tuning.Items.Objectives); // it plays again from the start
                    AtConsole = c;
                    break;
            }
        }

        void LeaveConsole()
        {
            AtConsole = null;
            Switches = null;
            Sequence = null;
        }

        /// <summary>Owner: the switch or sequence lock on the screen — number keys press its switches, Space / back leaves.</summary>
        void UpdateConsole()
        {
            var c = AtConsole;
            if (c == null) return;
            bool away = c.Solved || (c.transform.position - transform.position).magnitude > Items.SafeBox.Reach + 0.8f;
            if (away || Controls.Pressed(GameAction.Jump) || Controls.Pressed(GameAction.Back)) { LeaveConsole(); return; }
            var at = c.transform.position;
            int keys = Switches != null ? Switches.Count : Sequence != null ? Sequence.Lamps : 0;
            for (int i = 0; i < keys; i++)
            {
                if (!Controls.DigitPressed(i + 1)) continue;
                if (Switches != null)
                {
                    Audio.Sfx.At(Audio.Sfx.Bank?.Click, at, 0.7f, Audio.Sfx.Group.Items, 0.9f + 0.05f * i);
                    if (Switches.Press(i)) { SolveConsole(c); LeaveConsole(); return; }
                }
                else if (Sequence != null)
                {
                    switch (Sequence.Press(i, Time.time))
                    {
                        case Items.SequenceLock.Result.Wrong:
                            Audio.Sfx.At(Audio.Sfx.Bank?.Drop, at, 1f, Audio.Sfx.Group.Items, 0.6f);
                            EmitNoise(Tuning.Items.Objectives.WrongNoise);
                            ConsoleWrongTime = Time.time;
                            break;
                        case Items.SequenceLock.Result.Step:
                            Audio.Sfx.At(Audio.Sfx.Bank?.Click, at, 0.8f, Audio.Sfx.Group.Items, 0.9f + 0.08f * i);
                            break;
                        case Items.SequenceLock.Result.Opened:
                            SolveConsole(c);
                            LeaveConsole();
                            return;
                    }
                }
            }
        }

        /// <summary>Owner: a lock panel is solved — the server marks it (and opens the vault with the third).</summary>
        void SolveConsole(VaultConsole c)
        {
            if (c == null) return;
            Audio.Sfx.At(Audio.Sfx.Bank?.Stash, c.transform.position, 1f, Audio.Sfx.Group.Items);
            Link.SendUse(UseKind.VaultConsole, c.Index);
        }
    }
}
