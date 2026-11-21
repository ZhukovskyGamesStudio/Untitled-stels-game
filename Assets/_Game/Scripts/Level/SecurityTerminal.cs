using System.Collections.Generic;
using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A security terminal on a desk (<see cref="LevelLayout.Terminals"/>): the camera terminal shows what the house's
    /// cameras see, the hacking terminal hacks robots and devices from afar (a few charges per house), the map terminal shows
    /// the whole building (<see cref="UI.MapView.WholeHouse"/>). Built on every peer with the level; its screens glow in its
    /// colour (green cameras, blue hacking, amber map, dim when spent). The player
    /// uses it (<see cref="Player.PlayerController"/>); the server counts the hacks (<see cref="Game.GameManager"/>).
    /// </summary>
    public class SecurityTerminal : MonoBehaviour, IReachTarget
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
        [Tooltip("The monitors' screens of the console and of the kiosk (EyeScreen: UVs 0..1 on each): the kind's picture.")]
        public Renderer Pictures, AccessPictures;
        [Tooltip("What the screens show (Tools/Art/generate_screens.py): the camera feeds, hacking, the door's access, the floor plan.")]
        public Texture2D CamerasPicture, HackingPicture, DoorsPicture, MapPicture;


        public int Index { get; private set; } = -1;
        public TerminalKind Kind { get; private set; }
        public int Charges => Kind == TerminalKind.Hacking ? World.State.TerminalCharges(Index) : 0;
        /// <summary>Nothing left to do: a hacking terminal out of charges, a door terminal whose door is open.</summary>
        public bool Spent => Kind == TerminalKind.Hacking ? Charges <= 0 : Kind == TerminalKind.Doors && (AutoDoor.StorageDoor?.TerminalOpen ?? true);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly Color CamerasColor = new(0.3f, 1f, 0.45f), HackingColor = new(0.3f, 0.65f, 1f), DoorsColor = new(1f, 0.25f, 0.15f),
            MapColor = new(1f, 0.72f, 0.25f), SpentColor = new(0.25f, 0.27f, 0.3f);

        MaterialPropertyBlock m_Block;
        int m_Shown = -1;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Configure(int index, TerminalKind kind, bool kiosk = false)
        {
            Index = index;
            Kind = kind;
            // the door's terminal is a kiosk, never mistaken for the robots' console (and a map terminal with no desk to stand on)
            bool access = (kind == TerminalKind.Doors || kiosk) && Access != null;
            if (Console != null) Console.SetActive(!access);
            if (Access != null) Access.SetActive(access);
            if (access && AccessScreens != null) Screens = AccessScreens;
            if (access && AccessPictures != null) Pictures = AccessPictures;
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
            var c = Spent ? SpentColor : Kind switch
            {
                TerminalKind.Cameras => CamerasColor, TerminalKind.Doors => DoorsColor, TerminalKind.Map => MapColor, _ => HackingColor,
            };
            Screens.GetPropertyBlock(m_Block);
            m_Block.SetColor(BaseColorId, c);
            m_Block.SetColor(EmissionId, c * (Spent ? 0.3f : 1.6f));
            Screens.SetPropertyBlock(m_Block);
            if (Pictures == null) return;
            var picture = Kind switch
            {
                TerminalKind.Cameras => CamerasPicture, TerminalKind.Doors => DoorsPicture, TerminalKind.Map => MapPicture, _ => HackingPicture,
            };
            Pictures.GetPropertyBlock(m_Block);
            if (picture != null)
            {
                m_Block.SetTexture(BaseMapId, picture);
                m_Block.SetTexture(EmissionMapId, picture);
            }
            m_Block.SetColor(BaseColorId, c * 0.35f);
            m_Block.SetColor(EmissionId, c * (Spent ? 0.25f : 1.3f));
            Pictures.SetPropertyBlock(m_Block);
        }

        /// <summary>Where the player looks at it from: the side of the desk with the keyboard.</summary>
        public Vector3 Front => transform.position + transform.forward * 0.4f;

        /// <summary>The terminal a player can use from here (same floor, nothing in between), or null.</summary>
        public static SecurityTerminal Near(Vector3 player, Vector3 hands, float reach = Reach, Player.Aim? aim = null)
            => ReachSearch.Nearest(All, player, hands, reach, aim);

        bool IReachTarget.CanReach(Vector3 player) => Index >= 0;
        (Vector3, Vector3, float) IReachTarget.AimSpan => (transform.position + Vector3.up * 0.6f, transform.position + Vector3.up * 1.3f, 0.55f);
        Vector3 IReachTarget.ReachPoint => transform.position + Vector3.up * ReachSearch.Middle;
        float IReachTarget.MaxReach => float.PositiveInfinity;
        Transform IReachTarget.ReachRoot => transform;
        Vector3? IReachTarget.HandsTo => transform.position + Vector3.up * 0.9f;

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
