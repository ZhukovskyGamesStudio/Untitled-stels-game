using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A crate to search (prefab Props/Crate, <see cref="LevelLayout.Crates"/>): hold E, the lid opens (on every peer)
    /// and what was inside lies on the floor of the hollow box (<see cref="Game.GameManager.UseServer"/>), to take one by one.
    /// </summary>
    public class SearchCrate : Interactable
    {
        [Tooltip("The lid: swings open around its local X (the hinge at the back).")]
        public Transform Lid;

        /// <summary>The longest thing that lies inside (the box is 0.72 x 0.52 m inside, Tools/Blender/poi.py model_crate).</summary>
        public const float InsideLength = 0.68f;

        /// <summary>Where the n-th thing found lies, in the crate's space: three rows front to back, then a layer up (pure).</summary>
        public static Vector3 SlotInside(int n) => new Vector3(0f, 0.09f + (n / 3) * 0.08f, -0.16f + (n % 3) * 0.16f);


        public override UseKind Kind => UseKind.Crate;
        public override float HoldTime => Tuning.Items.Gadgets.CrateSearchTime;
        public override Vector3 Point => transform.position + Vector3.up * 0.5f;
        public override float AimRadius => 0.6f; // a shelf or a pallet

        Quaternion m_Rest;
        OpenTween m_Open;
        float m_Shown = -1f;

        void Awake()
        {
            if (Lid != null) m_Rest = Lid.localRotation;
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !World.IsOpened(OpenKeys.Crate(Index));
            return available ? Loc.T("use.crate") : Loc.T("use.crate.empty");
        }

        void Update()
        {
            if (Lid == null || Index < 0) return;
            m_Open.Step(World.IsOpened(OpenKeys.Crate(Index)), Time.deltaTime, 0.4f);
            if (Mathf.Approximately(m_Open.Value, m_Shown)) return;
            m_Shown = m_Open.Value;
            Lid.localRotation = m_Rest * Quaternion.Euler(-105f * m_Shown, 0f, 0f);
        }
    }
}
