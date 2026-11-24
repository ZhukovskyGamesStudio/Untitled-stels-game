using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A button of the lift: on a landing beside its doors (calls the cab to this floor) or on the cab's panel (sends it to
    /// its floor). <see cref="Interactable.Index"/> is the floor; the server sends the lift (<see cref="Game.GameManager.UseServer"/>).
    /// Its lamp glows while the lift goes to its floor, and while the cab stands there with its doors open.
    /// </summary>
    public class LiftButton : Interactable
    {
        [Tooltip("On the cab's panel (a floor button) rather than on a landing (a call button).")]
        public bool InCab;
        [Tooltip("The button's lamp.")]
        public Renderer Lamp;
        [Tooltip("The cab (a floor button): its own walls do not stand between the hands and the button.")]
        public Transform Cab;

        public override UseKind Kind => InCab ? UseKind.LiftFloor : UseKind.LiftCall;
        public override Vector3 Point => transform.position + transform.forward * 0.015f;
        public override float AimRadius => InCab ? 0.045f : 0.2f;
        public override float Reach => 1.5f;
        public override Transform ReachRoot => Cab != null ? Cab : transform;

        static float FloorTime => Configs.Tuning.Level.Building.LiftFloorTime;
        static float DoorTime => Configs.Tuning.Level.Building.LiftDoorTime;

        int m_Shown = -1;

        public override string Prompt(PlayerController player, out bool available)
        {
            var s = World.State.Lift;
            available = LiftRules.Phase(s, World.State.Now, FloorTime, DoorTime) == LiftPhase.Open && s.To != Index;
            return InCab ? Loc.F("use.lift_floor", Index + 1) : Loc.T("use.lift_call");
        }

        void Update()
        {
            if (Index < 0 || Lamp == null) return;
            var s = World.State.Lift;
            int lit = s.To == Index ? 1 : 0;
            if (lit == m_Shown) return;
            if (m_Shown >= 0 && lit == 1) Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.SwitchOn : null, Point, 0.7f, Audio.Sfx.Group.Items);
            m_Shown = lit;
            Lamp.enabled = lit == 1;
        }
    }
}
