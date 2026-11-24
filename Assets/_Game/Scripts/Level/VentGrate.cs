using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A grate in a room's ceiling (prefab Level/VentGrate, spots from <see cref="VentPlan"/>): the way into the storey's
    /// crawl ducts. Open, it is a hole in the ceiling: a player gets in from a way up under it (a chest-high piece and a step,
    /// <see cref="DuctGrate.ClimbDir"/>) with a crouched jump — once the feet are nearly at the duct's floor they are pulled
    /// in (<see cref="IntoDuct"/>) — and drops out by walking into it from the duct. Screwed shut, it is floor in the duct
    /// and ceiling below until a player with a screwdriver in hand takes its lid off (E, the server, for everyone).
    /// </summary>
    public class VentGrate : Interactable
    {
        [Tooltip("The grille: swings down on its hinge while the grate is open.")]
        public Transform Lid;

        public static readonly List<VentGrate> Grates = new();

        public override UseKind Kind => UseKind.VentGrate;
        /// <summary>Under the ceiling: reached from the floor below (the screwdriver) and from the duct above.</summary>
        public override Vector3 Point => transform.position + Vector3.down * 0.6f;
        public override float AimRadius => 0.35f;
        /// <summary>Right under it (or right over it in the duct): a lid is unscrewed from close by, not across the room.</summary>
        public override float Reach => 0.9f;

        /// <summary>The room under it.</summary>
        public int Room { get; private set; } = -1;
        /// <summary>Its lid was screwed shut when the house was built.</summary>
        public bool Closed { get; private set; }


        /// <summary>Open: never shut, or its lid taken off with a screwdriver.</summary>
        public bool Open => !Closed || World.IsOpened(OpenKeys.Vent(Index));

        /// <summary>Only a shut grate is used (E with a screwdriver in hand): an open one is climbed through, no key.</summary>
        public override bool Usable(PlayerController player) => !Open;

        Collider m_Floor;

        void Awake() => m_Floor = GetComponent<Collider>();

        protected override void OnEnable()
        {
            base.OnEnable();
            Grates.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Grates.Remove(this);
        }

        public void Setup(int index, int room, bool closed = false)
        {
            Room = room;
            Closed = closed;
            Configure(index);
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            // screwed shut: a screwdriver in hand takes the lid off (the server does it, for everyone)
            var inv = player.GetComponent<PlayerInventory>();
            available = inv != null && inv.Holds(Items.ItemCatalog.Screwdriver);
            return Loc.T(available ? "use.vent_unscrew" : "use.vent_shut");
        }

        /// <summary>
        /// A crouched body rising through an open grate gets in (pure): its feet within <paramref name="assist"/> under the
        /// duct's floor, inside the hole (the body's middle within its half width less a margin).
        /// </summary>
        public static bool IntoDuct(Vector3 feet, Vector3 grate, bool crouched, bool rising, float assist = 0.5f)
        {
            if (!crouched || !rising) return false;
            float floor = grate.y + LevelMetrics.CeilingBoard;
            if (feet.y < floor - assist || feet.y > floor + 0.3f) return false;
            var d = new Vector2(feet.x - grate.x, feet.z - grate.z);
            float half = VentPlan.Res / 2f - 0.15f;
            return Mathf.Abs(d.x) <= half && Mathf.Abs(d.y) <= half;
        }

        /// <summary>The open grate a crouched, rising body at <paramref name="feet"/> gets into the duct through, or null.</summary>
        public static VentGrate Under(Vector3 feet, bool crouched, bool rising)
        {
            foreach (var g in Grates)
                if (g.Open && IntoDuct(feet, g.transform.position, crouched, rising)) return g;
            return null;
        }

        OpenTween m_Open;

        void Update()
        {
            if (Index < 0) return;
            bool open = Open;
            // open: a hole (nothing to stand on); shut: floor in the duct, ceiling below
            if (m_Floor != null && m_Floor.enabled == open) m_Floor.enabled = !open;
            if (Lid == null) return;
            if (m_Open.Step(open, Time.deltaTime, 1f / 3f) == OpenMove.StartedOpening) // its lid swings down: every peer hears it
                Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.GrilleOff : null, transform.position, 0.8f, Audio.Sfx.Group.Items);
            Lid.localRotation = Quaternion.Euler(m_Open.Value * 85f, 0f, 0f);
        }
    }
}
