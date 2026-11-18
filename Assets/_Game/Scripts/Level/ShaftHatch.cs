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
            return Loc.T(World.IsOpened(OpenKeys.Shaft) ? "use.shaft_close" : "use.shaft_open");
        }

        OpenTween m_Open;

        void Update()
        {
            if (Lid == null) return;
            bool open = World.IsOpened(OpenKeys.Shaft);
            if (m_Open.Step(open, Time.deltaTime, SwingTime) != OpenMove.None) DoorSounds.Play(DoorSound.Heavy, transform.position, open);
            Lid.localRotation = Quaternion.Euler(m_Open.Smooth * OpenAngle, 0f, 0f);
            if (LidCollider != null) LidCollider.enabled = m_Open.Value < 0.1f;
            if (Link != null && Link.activated != open) Link.activated = open;
        }
    }
}
