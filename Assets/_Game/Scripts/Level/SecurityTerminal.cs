using System.Collections.Generic;
using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A security terminal on a desk (<see cref="LevelLayout.Terminals"/>): the camera terminal shows what the house's
    /// cameras see, the hacking terminal hacks robots and devices from afar (a few charges per house). Built on every
    /// peer with the level; its screens glow in its colour (green cameras, blue hacking, dim when spent). The player
    /// uses it (<see cref="Player.PlayerController"/>); the server counts the hacks (<see cref="Game.GameManager"/>).
    /// </summary>
    public class SecurityTerminal : MonoBehaviour
    {
        public static readonly List<SecurityTerminal> All = new();

        /// <summary>Stand this close (metres, horizontally) to use it.</summary>
        public const float Reach = 1.7f;

        [Tooltip("The glowing screens (kept out of the merged furniture by their property block).")]
        public Renderer Screens;
        [Tooltip("The robots' control console (hacking, cameras) and the storage door's access kiosk: the one for its kind shows.")]
        public GameObject Console, Access;
        [Tooltip("The kiosk's screen and beacon.")]
        public Renderer AccessScreens;

        /// <summary>Hacks left on a terminal (set by the game: index in the layout's props → charges left).</summary>
        public static System.Func<int, int> ChargesLeft = _ => 0;

        public int Index { get; private set; } = -1;
        public TerminalKind Kind { get; private set; }
        public int Charges => Kind == TerminalKind.Hacking ? ChargesLeft(Index) : 0;
        /// <summary>Nothing left to do: a hacking terminal out of charges, a door terminal whose door is open.</summary>
        public bool Spent => Kind == TerminalKind.Hacking ? Charges <= 0 : Kind == TerminalKind.Doors && (AutoDoor.StorageDoor?.TerminalOpen ?? true);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color CamerasColor = new(0.3f, 1f, 0.45f), HackingColor = new(0.3f, 0.65f, 1f), DoorsColor = new(1f, 0.25f, 0.15f),
            SpentColor = new(0.25f, 0.27f, 0.3f);

        MaterialPropertyBlock m_Block;
        int m_Shown = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Configure(int index, TerminalKind kind)
        {
            Index = index;
            Kind = kind;
            // the door's terminal is a kiosk, never mistaken for the robots' console
            bool access = kind == TerminalKind.Doors && Access != null;
            if (Console != null) Console.SetActive(!access);
            if (Access != null) Access.SetActive(access);
            if (access && AccessScreens != null) Screens = AccessScreens;
            m_Shown = -1;
            Show();
        }

        void Update() => Show();

        void Show()
        {
            int state = (int)Kind * 2 + (Spent ? 1 : 0);
            if (state == m_Shown || Screens == null) return;
            m_Shown = state;
            m_Block ??= new MaterialPropertyBlock();
            var c = Spent ? SpentColor : Kind switch { TerminalKind.Cameras => CamerasColor, TerminalKind.Doors => DoorsColor, _ => HackingColor };
            Screens.GetPropertyBlock(m_Block);
            m_Block.SetColor(BaseColorId, c);
            m_Block.SetColor(EmissionId, c * (Spent ? 0.3f : 1.6f));
            Screens.SetPropertyBlock(m_Block);
        }

        /// <summary>Where the player looks at it from: the side of the desk with the keyboard.</summary>
        public Vector3 Front => transform.position + transform.forward * 0.4f;

        /// <summary>The terminal a player can use from here (same floor, nothing in between), or null.</summary>
        public static SecurityTerminal Near(Vector3 player, Vector3 hands, float reach = Reach, Player.Aim? aim = null)
        {
            SecurityTerminal best = null;
            float bestD = reach;
            foreach (var t in All)
            {
                if (t.Index < 0 || Mathf.Abs(player.y - t.transform.position.y) > 1.6f) continue;
                var p = t.transform.position;
                if (aim.HasValue && !aim.Value.At(p + Vector3.up * 0.6f, p + Vector3.up * 1.3f, 0.55f)) continue; // looked at
                var to = player - t.transform.position;
                to.y = 0;
                if (to.magnitude > bestD || !Perception.ReachClear(hands, t.transform.position + Vector3.up * 0.9f, t.transform)) continue;
                best = t;
                bestD = to.magnitude;
            }
            return best;
        }

        public static SecurityTerminal Find(int index)
        {
            foreach (var t in All) if (t.Index == index) return t;
            return null;
        }

        /// <summary>
        /// What the terminal can look at, nearest first: the cameras (camera terminal) or every robot and device that is not
        /// hacked already (hacking terminal). Pure apart from reading the detectors' positions.
        /// </summary>
        public static void Targets(TerminalKind kind, Vector3 from, List<Detector> into)
        {
            into.Clear();
            foreach (var d in Detector.All)
            {
                bool camera = d.Kind == RobotKind.Camera;
                if (kind == TerminalKind.Cameras ? camera : !d.IsHacked) into.Add(d);
            }
            into.Sort((a, b) => (a.Position - from).sqrMagnitude.CompareTo((b.Position - from).sqrMagnitude));
        }
    }
}
