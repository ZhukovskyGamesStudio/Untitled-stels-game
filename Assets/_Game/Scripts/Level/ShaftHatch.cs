using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The lid over the shaft in the lobby's floor (prefab Level/ShaftHatch), the way down to the basement the team arrives
    /// in (<see cref="Basement"/>). E opens or closes it for everyone (the server keeps it: <see cref="IsOpen"/>, a key in
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

        /// <summary>The lid is open (set by the game: the server's word).</summary>
        public static System.Func<bool> IsOpen = () => false;

        public override UseKind Kind => UseKind.ShaftHatch;
        /// <summary>The lid's middle: looking anywhere at it (from the lobby or from the ladder under it) takes it.</summary>
        public override Vector3 Point => transform.position + Vector3.up * 0.05f;
        public override float AimRadius => Basement.ShaftHalf + 0.25f;

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T(IsOpen() ? "use.shaft_close" : "use.shaft_open");
        }

        float m_Open;

        void Update()
        {
            if (Lid == null) return;
            bool open = IsOpen();
            float before = m_Open;
            m_Open = Mathf.MoveTowards(m_Open, open ? 1f : 0f, Time.deltaTime / SwingTime);
            if (!Mathf.Approximately(before, m_Open) && (m_Open == 0f || m_Open == 1f))
                Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.DoorSlide : null, transform.position, 0.8f, Audio.Sfx.Group.Items, open ? 0.7f : 0.6f);
            Lid.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, 1f, m_Open) * OpenAngle, 0f, 0f);
            if (LidCollider != null) LidCollider.enabled = m_Open < 0.1f;
            if (Link != null && Link.activated != open) Link.activated = open;
        }
    }
}
