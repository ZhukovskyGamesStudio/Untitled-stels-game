using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The grille over a crawl way (on the prefabs Level/VentFrame and HighVentFrame): every vent starts shut by its grille
    /// — hold E to work it loose (<see cref="InteractionSettings.VentGrilleTime"/>), then it leans against the wall beside the
    /// hole and the way is open, for the whole team. <see cref="Interactable.Index"/> is the vent's doorway (index in
    /// <see cref="LevelLayout.Doorways"/>); the server keeps which grilles are off (<see cref="Game.GameManager.UseServer"/>),
    /// every peer shows it.
    /// </summary>
    public class VentGrille : Interactable
    {
        [Tooltip("The grille in the hole (with its collider: nobody crawls through it).")]
        public GameObject Mounted;
        [Tooltip("The same grille taken off, leaning against the wall beside the hole.")]
        public GameObject Removed;
        [Tooltip("The middle of the hole (where the hands go).")]
        public Transform Hole;


        public override UseKind Kind => UseKind.VentGrille;
        public override float HoldTime => Configs.Tuning.Player.Interaction.VentGrilleTime;
        public override Vector3 Point => Hole != null ? Hole.position : transform.position + Vector3.up * 0.9f;
        public override float AimRadius => 0.6f;
        public override float Reach => 1.6f;

        public bool Open => Index >= 0 && World.IsOpened(OpenKeys.Crawl(Index));

        int m_Shown = -1;

        public override string Prompt(PlayerController player, out bool available)
        {
            // screwed on: only a screwdriver in hand works it loose (the server checks it too)
            var inv = player.GetComponent<PlayerInventory>();
            available = !Open && inv != null && inv.Holds(Items.ItemCatalog.Screwdriver);
            return Loc.T(Open || available ? "use.vent_grille" : "use.vent_grille_shut");
        }

        void Update()
        {
            int open = Open ? 1 : 0;
            if (open == m_Shown) return;
            if (m_Shown == 0 && open == 1) Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.GrilleOff : null, Point, 0.8f, Audio.Sfx.Group.Items);
            m_Shown = open;
            if (Mounted != null) Mounted.SetActive(open == 0);
            if (Removed != null) Removed.SetActive(open == 1);
        }
    }
}
