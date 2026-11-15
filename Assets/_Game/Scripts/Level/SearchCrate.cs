using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A crate to search (prefab Props/Crate, <see cref="LevelLayout.Crates"/>): hold E and the server lays out what was
    /// inside in front of it (<see cref="Game.GameManager.UseServer"/>); the lid stays open on every peer.
    /// </summary>
    public class SearchCrate : Interactable
    {
        [Tooltip("The lid: swings open around its local X (the hinge at the back).")]
        public Transform Lid;

        /// <summary>Which crates are searched already (set by the game: prop index → searched).</summary>
        public static System.Func<int, bool> IsSearched = _ => false;

        public override UseKind Kind => UseKind.Crate;
        public override float HoldTime => Tuning.Items.Gadgets.CrateSearchTime;
        public override Vector3 Point => transform.position + Vector3.up * 0.5f;
        public override float AimRadius => 0.6f; // a shelf or a pallet

        Quaternion m_Rest;
        float m_Open;

        void Awake()
        {
            if (Lid != null) m_Rest = Lid.localRotation;
        }

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !IsSearched(Index);
            return available ? Loc.T("use.crate") : Loc.T("use.crate.empty");
        }

        void Update()
        {
            if (Lid == null || Index < 0) return;
            float target = IsSearched(Index) ? 1f : 0f;
            if (Mathf.Approximately(m_Open, target)) return;
            m_Open = Mathf.MoveTowards(m_Open, target, Time.deltaTime * 2.5f);
            Lid.localRotation = m_Rest * Quaternion.Euler(-105f * m_Open, 0f, 0f);
        }
    }
}
