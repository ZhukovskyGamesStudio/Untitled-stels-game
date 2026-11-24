using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The push button on the wall inside the storage room, beside its door: a press opens the door for whoever is locked
    /// in, and it locks again behind them (<see cref="AutoDoor.StorageLocked"/>, the server's <c>EscapeStorageServer</c>).
    /// Built on every peer with the house; its lamp and its click follow the door's replicated state.
    /// </summary>
    public class ExitButton : Interactable
    {
        [Tooltip("The button's cap: pressed in while the door is held open from inside.")]
        public Transform Cap;
        [Tooltip("The cap's own glow: green while it can be pressed.")]
        public Renderer Lamp;

        /// <summary>The door it opens (<see cref="AutoDoor.Index"/>).</summary>
        public int Door { get; private set; } = -1;

        public override UseKind Kind => UseKind.ExitButton;
        public override Vector3 Point => transform.position + transform.forward * 0.03f;
        public override float AimRadius => 0.3f;

        int m_Shown = -1;
        Vector3 m_CapOut;

        /// <summary>Where the button goes: beside the doorway on the room's side of the wall, at a hand's height (pure).</summary>
        public static Vector3 Spot(Vector3 door, Vector3 inward, Vector3 along, float clear, float side) =>
            door + inward * (LevelMetrics.WallThickness / 2f + 0.012f) + along * (side * (clear / 2f + 0.3f)) + Vector3.up * 1.15f;

        public void Setup(int index, int door)
        {
            Door = door;
            if (Cap != null) m_CapOut = Cap.localPosition;
            Configure(index);
        }

        /// <summary>The door is held open from inside now.</summary>
        public bool Pressed => Door >= 0 && World.IsOpened(OpenKeys.Escape(Door));

        public override string Prompt(PlayerController player, out bool available)
        {
            available = !Pressed;
            return Loc.T("use.exit_button");
        }

        void Update()
        {
            if (Index < 0) return;
            int on = Pressed ? 1 : 0;
            if (on == m_Shown) return;
            if (m_Shown >= 0 && on == 1) Audio.Sfx.At(Audio.Sfx.Bank == null ? null : Audio.Sfx.Bank.SwitchOn, Point, 1f, Audio.Sfx.Group.Items, 0.8f);
            m_Shown = on;
            if (Cap != null) Cap.localPosition = m_CapOut - Vector3.forward * (on == 1 ? 0.008f : 0f);
            if (Lamp != null) Lamp.enabled = on == 0;
        }
    }
}
