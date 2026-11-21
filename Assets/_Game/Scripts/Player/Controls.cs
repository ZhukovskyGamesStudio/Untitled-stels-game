using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>Everything the player can press. Mouse look, dragging items with the left button and Esc are fixed.</summary>
    public enum GameAction
    {
        Forward, Back, Left, Right, Sprint, Jump, CrouchHold, CrouchToggle,
        Interact, Hands, Rotate, Mirror, Hatch, StashAll, Ping, Mark, Map, Style, Unstuck, Restart, Cheats, Gadget, Horn,
        UseHand,
        // the team's storage robot: come to me; go to the exit through the ducts
        BotCall, BotExit,
        // knocked out: call the team (seen through walls)
        CallHelp,
        // voice chat: hold to talk (when the microphone is set to push-to-talk)
        PushToTalk,
    }

    /// <summary>
    /// A key or a mouse button: keyboard keys are <see cref="Key"/> values (positive), mouse buttons are negative
    /// (-1 left, -2 right, -3 middle, -4 forward, -5 back). 0 = unbound.
    /// </summary>
    [Serializable]
    public struct KeyBinding : IEquatable<KeyBinding>
    {
        public int Code;

        public KeyBinding(int code) => Code = code;
        public static KeyBinding Of(Key key) => new((int)key);
        public static KeyBinding MouseButton(int index) => new(-(index + 1));

        public bool IsMouse => Code < 0;
        public bool IsNone => Code == 0;
        public Key Key => Code > 0 ? (Key)Code : Key.None;
        /// <summary>0 left, 1 right, 2 middle, 3 forward, 4 back; -1 for keys.</summary>
        public int MouseIndex => Code < 0 ? -Code - 1 : -1;

        public bool Equals(KeyBinding o) => Code == o.Code;
        public override bool Equals(object obj) => obj is KeyBinding o && Equals(o);
        public override int GetHashCode() => Code;
        public override string ToString() => Controls.Name(this);
    }

    /// <summary>
    /// Rebindable controls: the defaults, the player's choice (PlayerPrefs) and reading them from the devices.
    /// Game code asks <see cref="Held"/> / <see cref="Pressed"/> for an action, never for a key; texts for the player
    /// use <see cref="Label"/> so that prompts follow the rebinds.
    /// </summary>
    public static class Controls
    {
        public readonly struct Info
        {
            public readonly GameAction Action;
            /// <summary>The group's id ("move", "actions", "team", "car", "other"): the table key is <c>controls.group.&lt;id&gt;</c>.</summary>
            public readonly string Group;
            public readonly KeyBinding Default;

            public Info(GameAction action, string group, KeyBinding binding)
            {
                Action = action;
                Group = group;
                Default = binding;
            }

            /// <summary>The action's name for the controls page (table key <c>action.&lt;action&gt;</c>).</summary>
            public string Title => Loc.T(TitleKey(Action));
            public string GroupTitle => Loc.T(GroupKey(Group));
        }

        public static string TitleKey(GameAction a) => "action." + a.ToString().ToLowerInvariant();

        /// <summary>The table key of a group of <see cref="Info.Group"/>: "controls.group.move".</summary>
        public static string GroupKey(string group) => "controls.group." + group;

        /// <summary>Every action in the order the controls page lists them (index = (int)GameAction).</summary>
        public static readonly Info[] All =
        {
            new(GameAction.Forward, "move", KeyBinding.Of(Key.W)),
            new(GameAction.Back, "move", KeyBinding.Of(Key.S)),
            new(GameAction.Left, "move", KeyBinding.Of(Key.A)),
            new(GameAction.Right, "move", KeyBinding.Of(Key.D)),
            new(GameAction.Sprint, "move", KeyBinding.Of(Key.LeftShift)),
            new(GameAction.Jump, "move", KeyBinding.Of(Key.Space)),
            new(GameAction.CrouchHold, "move", KeyBinding.Of(Key.LeftCtrl)),
            new(GameAction.CrouchToggle, "move", KeyBinding.Of(Key.C)),
            new(GameAction.Interact, "actions", KeyBinding.Of(Key.E)),
            new(GameAction.Hands, "actions", KeyBinding.Of(Key.Tab)),
            new(GameAction.Rotate, "actions", KeyBinding.Of(Key.R)),
            new(GameAction.Mirror, "actions", KeyBinding.Of(Key.T)),
            new(GameAction.Hatch, "actions", KeyBinding.Of(Key.F)),
            new(GameAction.StashAll, "actions", KeyBinding.Of(Key.G)),
            new(GameAction.Ping, "team", KeyBinding.MouseButton(2)),
            new(GameAction.Mark, "team", KeyBinding.Of(Key.Q)),
            new(GameAction.Map, "team", KeyBinding.Of(Key.M)),
            new(GameAction.Style, "other", KeyBinding.Of(Key.F8)),
            new(GameAction.Unstuck, "other", KeyBinding.Of(Key.K)),
            new(GameAction.Restart, "other", KeyBinding.Of(Key.R)),
            new(GameAction.Cheats, "other", KeyBinding.Of(Key.F4)),
            new(GameAction.Gadget, "actions", KeyBinding.Of(Key.X)),
            new(GameAction.Horn, "car", KeyBinding.Of(Key.H)),
            new(GameAction.UseHand, "actions", KeyBinding.Of(Key.V)),
            new(GameAction.BotCall, "team", KeyBinding.Of(Key.B)),
            new(GameAction.BotExit, "team", KeyBinding.Of(Key.N)),
            new(GameAction.CallHelp, "team", KeyBinding.Of(Key.Z)),
            new(GameAction.PushToTalk, "team", KeyBinding.Of(Key.CapsLock)),
        };

        const string Prefix = "stealth.key.";
        static readonly KeyBinding[] s_Bindings = Defaults();

        static KeyBinding[] Defaults()
        {
            var b = new KeyBinding[All.Length];
            for (int i = 0; i < All.Length; i++) b[i] = All[i].Default;
            return b;
        }

        /// <summary>Grows with every change of the bindings (texts that name keys rebuild).</summary>
        public static int Version { get; private set; }

        public static KeyBinding Get(GameAction a) => s_Bindings[(int)a];

        public static void Set(GameAction a, KeyBinding binding)
        {
            s_Bindings[(int)a] = binding;
            Version++;
        }

        public static void ResetToDefaults()
        {
            for (int i = 0; i < All.Length; i++) s_Bindings[i] = All[i].Default;
            Version++;
        }

        /// <summary>The four movement keys: "WASD" when they are letters, otherwise "↑/←/↓/→"-style.</summary>
        public static string MoveLabel()
        {
            string f = Label(GameAction.Forward), l = Label(GameAction.Left), b = Label(GameAction.Back), r = Label(GameAction.Right);
            return f.Length == 1 && l.Length == 1 && b.Length == 1 && r.Length == 1 ? f + l + b + r : $"{f}/{l}/{b}/{r}";
        }

        /// <summary>Other actions on the same key. Some share it on purpose (R rotates in the backpack and restarts a failed run).</summary>
        public static bool Conflicts(GameAction a, out GameAction other)
        {
            var b = Get(a);
            for (int i = 0; i < All.Length; i++)
            {
                other = (GameAction)i;
                if (other == a || b.IsNone || !s_Bindings[i].Equals(b) || SharedOnPurpose(a, other)) continue;
                return true;
            }
            other = a;
            return false;
        }

        static bool SharedOnPurpose(GameAction a, GameAction b) =>
            (a == GameAction.Rotate && b == GameAction.Restart) || (a == GameAction.Restart && b == GameAction.Rotate);

        public static void Load()
        {
            for (int i = 0; i < All.Length; i++)
                s_Bindings[i] = new KeyBinding(PlayerPrefs.GetInt(Prefix + All[i].Action, All[i].Default.Code));
            Version++;
        }

        public static void Save()
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (s_Bindings[i].Equals(All[i].Default)) PlayerPrefs.DeleteKey(Prefix + All[i].Action);
                else PlayerPrefs.SetInt(Prefix + All[i].Action, s_Bindings[i].Code);
            }
        }

        // ------------------------------------------------------------------ reading the devices

        /// <summary>The binding is down now.</summary>
        public static bool Held(GameAction a) => IsDown(Get(a));

        /// <summary>The binding went down this frame.</summary>
        public static bool Pressed(GameAction a) => WentDown(Get(a));

        /// <summary>
        /// A number key went down this frame (1–9, the top row or the keypad): the switches of a lock panel. Fixed keys,
        /// not an action to rebind — the panel draws the numbers on its lamps.
        /// </summary>
        public static bool DigitPressed(int n)
        {
            var kb = Keyboard.current;
            if (kb == null || n < 1 || n > 9) return false;
            var top = kb[(Key)((int)Key.Digit1 + n - 1)];
            var pad = kb[(Key)((int)Key.Numpad1 + n - 1)];
            return (top != null && top.wasPressedThisFrame) || (pad != null && pad.wasPressedThisFrame);
        }

        public static bool IsDown(KeyBinding b)
        {
            var c = Control(b);
            return c != null && c.isPressed;
        }

        public static bool WentDown(KeyBinding b)
        {
            var c = Control(b);
            return c != null && c.wasPressedThisFrame;
        }

        static UnityEngine.InputSystem.Controls.ButtonControl Control(KeyBinding b)
        {
            if (b.IsNone) return null;
            if (!b.IsMouse)
            {
                var kb = Keyboard.current;
                if (kb == null || b.Key <= Key.None) return null;
                try { return kb[b.Key]; }
                catch (ArgumentOutOfRangeException) { return null; } // a key this keyboard does not have
            }
            var m = Mouse.current;
            if (m == null) return null;
            return b.MouseIndex switch
            {
                0 => m.leftButton,
                1 => m.rightButton,
                2 => m.middleButton,
                3 => m.forwardButton,
                4 => m.backButton,
                _ => null,
            };
        }

        /// <summary>
        /// Rebinding: the key or mouse button pressed this frame, if any. Esc is never captured (it closes the menu);
        /// the left mouse button is ignored too — it clicks the buttons of the menu itself.
        /// </summary>
        public static bool CapturePress(out KeyBinding binding)
        {
            binding = default;
            var kb = Keyboard.current;
            if (kb != null)
                foreach (var k in kb.allKeys)
                {
                    if (k == null || !k.wasPressedThisFrame || k.keyCode == Key.Escape) continue;
                    binding = KeyBinding.Of(k.keyCode);
                    return true;
                }
            var m = Mouse.current;
            if (m == null) return false;
            for (int i = 1; i <= 4; i++)
            {
                var b = KeyBinding.MouseButton(i);
                if (!WentDown(b)) continue;
                binding = b;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ names

        /// <summary>Short name of the action's key for prompts: "E", "Space", "MMB" (in the player's language).</summary>
        public static string Label(GameAction a) => Name(Get(a));

        public static string Name(KeyBinding b)
        {
            if (b.IsNone) return "—";
            if (b.IsMouse)
                return b.MouseIndex switch
                {
                    0 => Loc.T("key.lmb"),
                    1 => Loc.T("key.rmb"),
                    2 => Loc.T("key.mmb"),
                    3 => Loc.T("key.mouse4"),
                    4 => Loc.T("key.mouse5"),
                    _ => Loc.T("key.mouse"),
                };
            var k = b.Key;
            switch (k)
            {
                case Key.Space: return Loc.T("key.space");
                case Key.LeftShift: return "Shift";
                case Key.RightShift: return Loc.T("key.rshift");
                case Key.LeftCtrl: return "Ctrl";
                case Key.RightCtrl: return Loc.T("key.rctrl");
                case Key.LeftAlt: return "Alt";
                case Key.RightAlt: return Loc.T("key.ralt");
                case Key.Enter: return "Enter";
                case Key.Backspace: return "Backspace";
                case Key.Backquote: return "`";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                case Key.LeftBracket: return "[";
                case Key.RightBracket: return "]";
                case Key.Semicolon: return ";";
                case Key.Quote: return "'";
                case Key.Comma: return ",";
                case Key.Period: return ".";
                case Key.Slash: return "/";
                case Key.Backslash: return "\\";
                case Key.UpArrow: return "↑";
                case Key.DownArrow: return "↓";
                case Key.LeftArrow: return "←";
                case Key.RightArrow: return "→";
                case Key.CapsLock: return "Caps Lock";
            }
            var s = k.ToString();
            if (s.StartsWith("Digit")) return s.Substring(5);
            if (s.StartsWith("Numpad")) return "Num " + s.Substring(6);
            return s;
        }
    }
}
