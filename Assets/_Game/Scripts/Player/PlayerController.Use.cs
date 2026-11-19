using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>The player: in the car, and what E uses (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        // ------------------------------------------------------------------ in the car

        /// <summary>The seat of the car this player sits on (0 the driver's … 3), −1 = on foot (from the car's replicated seats).</summary>
        public int SeatIndex => Vehicles.Car.Active != null ? Vehicles.Car.Active.SeatOf(ClientId) : -1;
        public bool Driving => SeatIndex == 0;

        /// <summary>Feet this far under the seat's cushion: a sitting player's eyes are ~0.8 m above it.</summary>
        const float SeatDrop = 0.72f;
        /// <summary>
        /// The model's feet this far under the cushion when sitting: its hips (the legs' pivot, 0.92 m over the feet,
        /// <c>generate_models.LEG_PIVOT</c>) rest on the seat, so the head stays under the cab's roof (1.85 m).
        /// </summary>
        const float ModelUnderSeat = 0.85f;
        bool m_WasSeated;
        int m_LastSeat;

        /// <summary>
        /// Owner: sitting in the car the player is carried by it (looking around freely, turning with it); E gets out on the
        /// car's side. The server decides the seats (<see cref="Vehicles.Car.SitServer"/>).
        /// </summary>
        void UpdateSeat(bool menu)
        {
            var car = Vehicles.Car.Active;
            int seat = SeatIndex;
            bool seated = seat >= 0 && car.Seats[seat] != null;
            if (seated != m_WasSeated)
            {
                m_WasSeated = seated;
                // getting out beside the car
                if (!seated && car != null) TeleportOwner(car.ExitPoint(m_LastSeat), transform.rotation);
                if (seated) { transform.rotation = car.transform.rotation; Motor.SetPitch(6f); } // sitting down: eyes on the road
                m_Controller.enabled = !seated;
                Audio.Sfx.At(Audio.Sfx.Bank?.Land, transform.position, 0.6f, Audio.Sfx.Group.Footsteps);
            }
            if (!seated) return;
            m_LastSeat = seat;
            transform.position = car.Seats[seat].position - Vector3.up * SeatDrop;
            transform.Rotate(0f, car.YawDelta, 0f);
            if (!menu && !CursorMode && Controls.Pressed(GameAction.Interact))
            {
                // this press is spent: on the host the seat is freed at once, and the same E must not sit us down again
                // on the seat we are still standing on (UpdateUse runs later this frame)
                m_UseWaitRelease = true;
                Link.SendUse(UseKind.CarSeat, seat);
            }
        }

        /// <summary>Something in reach to use with E (a crate, the med table, a seat of the car…), or null.</summary>
        public Interactable UseTarget { get; private set; }
        /// <summary>The hint for <see cref="UseTarget"/>, whether E does something now, and the hold progress (0..1).</summary>
        public string UsePrompt { get; private set; }
        public bool UseAvailable { get; private set; }
        public float UseProgress { get; private set; }
        readonly HoldProgress<Interactable> m_UseHeld = new();
        bool m_UseWaitRelease;

        /// <summary>Owner: press (or hold) E at a crate, the med table, the workbench, a key lock, a lever, the car.</summary>
        void UpdateUse(bool holdE)
        {
            bool busy = ReviveTarget != null || HackTarget != null || DoorTarget != null || SafeTarget != null || TerminalTarget != null
                        || Cracking != null || AtConsole != null || CursorMode || SeatIndex >= 0;
            UseTarget = busy ? null : Interactable.Near(this, Hands, View);
            bool available = false;
            UsePrompt = UseTarget != null ? UseTarget.Prompt(this, out available) : null;
            UseAvailable = UseTarget != null && available;
            if (!holdE) m_UseWaitRelease = false;
            if (UseTarget == null || !UseAvailable || m_UseWaitRelease) { UseProgress = 0f; return; }
            float hold = UseTarget.HoldTime;
            bool done = hold <= 0f ? Controls.Pressed(GameAction.Interact) : holdE && m_UseHeld.Hold(UseTarget, Time.deltaTime, hold);
            UseProgress = hold > 0f ? m_UseHeld.Of(UseTarget) / hold : 0f; // the ring is a share of the hold, not seconds
            if (!done) return;
            m_UseWaitRelease = true; // one use per press
            if (UseTarget is VaultConsole console) { StartConsole(console); return; } // its minigame, here; the server hears when solved
            Link.SendUse(UseTarget.Kind, UseTarget.Index);
        }

        /// <summary>Server: the med table raised this player's health.</summary>
        public void HealServer(int hp)
        {
            m_Health.Heal(hp);
            PublishHealth();
        }

        /// <summary>Owner: hide in the hatch or climb out (also a test hook).</summary>
        public void SetHatch(bool hide)
        {
            Link.SendHatch(hide);
        }

        /// <summary>The drawer cabinet / lockers looked at and which part (E opens or closes it), or null.</summary>
        public Items.Storage StorageTarget { get; private set; }
        public int StoragePart { get; private set; } = -1;

        /// <summary>Owner: look right at a drawer or a locker door and press E to open or close it (the server decides).</summary>
        void UpdateStorage()
        {
            StorageTarget = null;
            StoragePart = -1;
            if (ReviveTarget != null || HackTarget != null || DoorTarget != null || SafeTarget != null || Cracking != null || Camera == null) return;
            var storage = LookTarget.At<Items.Storage>(Camera.transform, transform, m_Interaction.LookReach, m_Interaction.LookRadius, out var hit);
            if (storage == null || storage.Index < 0) return;
            StorageTarget = storage;
            StoragePart = storage.PartOf(hit.collider, hit.point);
            if (StoragePart < 0 || CursorMode || !Controls.Pressed(GameAction.Interact)) return;
            Link.SendUse(UseKind.StoragePart, OpenKeys.StoragePart(storage.Index, StoragePart));
        }

        /// <summary>Locked door in reach (hold E to hack), or null.</summary>
        public AutoDoor DoorTarget { get; private set; }

        /// <summary>At the door terminal, looking at it: the storage room's door, while it is locked (hold E opens it from here).</summary>
        AutoDoor StorageDoorAtTerminal()
        {
            var t = SecurityTerminal.Near(transform.position, Hands, SecurityTerminal.Reach, View);
            if (t == null || t.Kind != TerminalKind.Doors) return null;
            var d = AutoDoor.StorageDoor;
            return d != null && !d.TerminalOpen ? d : null; // even while a worker holds it open: unlocking it is for good
        }
        public float DoorProgress { get; private set; }
        public float DoorShare => DoorProgress / m_Interaction.DoorHackTime;
    }
}
