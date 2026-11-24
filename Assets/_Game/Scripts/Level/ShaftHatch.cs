using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The lid over the shaft in the lobby's floor (prefab Level/ShaftHatch), the way down to the basement the team arrives
    /// in (<see cref="Basement"/>). E opens or closes it for everyone (the server keeps it: <see cref="World"/>, a key in
    /// GameManager.Opened); closed it is floor to walk on, open it swings up on its hinge and the ladder below is free
    /// (<see cref="ShaftLadder"/>).
    /// </summary>
    public class ShaftHatch : Interactable
    {
        [Tooltip("The lid: turns about its local X on the hinge edge (−Z).")]
        public Transform Lid;
        [Tooltip("Solid while the lid is (almost) shut.")]
        public Collider LidCollider;
        [Tooltip("The exit hatch's lid (its shaft goes down to the passage in the sewer), else the entry's.")]
        public bool Exit;
        [Tooltip("Lamps in the frame's corners, lit in the hatch's colour (the map's).")]
        public Renderer[] Glow;

        /// <summary>The key it opens by (<see cref="OpenKeys"/>).</summary>
        public int Key => Exit ? OpenKeys.ExitShaft : OpenKeys.Shaft;

        /// <summary>The navmesh link up the shaft (set by the builder): open only while the lid is.</summary>
        [System.NonSerialized] public Unity.AI.Navigation.NavMeshLink Link;
        public const float OpenAngle = -105f, SwingTime = 0.6f;


        public override UseKind Kind => UseKind.ShaftHatch;
        /// <summary>The lid's middle: looking anywhere at it (from the lobby or from the ladder under it) takes it.</summary>
        public override Vector3 Point => transform.position + Vector3.up * 0.05f;
        public override float AimRadius => Basement.ShaftHalf + 0.25f;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T(World.IsOpened(Key) ? "use.shaft_close" : "use.shaft_open");
        }

        OpenTween m_Open;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor");

        Quaternion m_LidBase = Quaternion.identity;
        float m_OpenAngle = OpenAngle;
        bool m_Hinged;

        void Start()
        {
            if (Glow == null) return;
            var block = new MaterialPropertyBlock();
            var c = Exit ? Hatch.ExitColor : Hatch.EntryColor;
            foreach (var r in Glow)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, c);
                block.SetColor(EmissionId, c * 2.4f);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>
        /// The exit's lid hinges on the side with the nearest wall (its frame's turn follows the shaft below, not the room):
        /// opened, it stands back against that wall instead of rising like a dark slab in the room before the hole. Not on
        /// the climber's side (+Z, where one steps off the ladder). Close to the wall it opens to just past upright.
        /// </summary>
        void HingeAgainstWall()
        {
            if (Lid == null) return;
            var from = transform.position + Vector3.up * 0.6f;
            float half = Basement.ShaftHalf;
            float best = float.PositiveInfinity, yaw = 0f;
            // the sides a hinge can take (local): −Z as built, −X, +X; the yaw that turns the built hinge (−Z) there
            foreach (var (dir, turn) in new[] { (Vector3.back, 0f), (Vector3.left, 90f), (Vector3.right, -90f) })
            {
                var world = transform.TransformDirection(dir);
                int n = Physics.RaycastNonAlloc(from, world, s_Hits, half + 1.5f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (s_Hits[i].collider.transform.IsChildOf(transform) || s_Hits[i].collider.GetComponentInParent<PlayerController>() != null) continue;
                    if (s_Hits[i].distance < best) { best = s_Hits[i].distance; yaw = turn; }
                }
            }
            if (float.IsInfinity(best)) return; // open floor all round: as built
            m_LidBase = Quaternion.Euler(0f, yaw, 0f);
            var p = Lid.localPosition;
            Lid.localPosition = m_LidBase * p;
            // the lid's top swings past the hinge when opened beyond upright: not into a near wall
            float room = best - half;
            m_OpenAngle = room < 0.5f ? -92f : OpenAngle;
        }

        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        void Update()
        {
            if (Lid == null) return;
            bool open = World.IsOpened(Key);
            // (decided the first time it opens: by then the house round it is built)
            if (Exit && !m_Hinged && open && m_Open.Value <= 0f) { m_Hinged = true; HingeAgainstWall(); }
            var move = m_Open.Step(open, Time.deltaTime, SwingTime);
            if (move == OpenMove.StartedOpening) Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.HatchOpen : null, transform.position, 0.8f, Audio.Sfx.Group.Items);
            else if (move == OpenMove.StartedShutting) DoorSounds.Play(DoorSound.Heavy, transform.position, false);
            Lid.localRotation = m_LidBase * Quaternion.Euler(m_Open.Smooth * m_OpenAngle, 0f, 0f);
            if (LidCollider != null) LidCollider.enabled = m_Open.Value < 0.1f;
            if (Link != null && Link.activated != open) Link.activated = open;
        }
    }
}
