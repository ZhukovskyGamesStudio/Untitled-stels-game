using Stealth.Player;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A light switch beside a doorway (prefab Level/LightSwitch, spots from <see cref="UtilityPlan.SwitchSpots"/>): E switches
    /// the ceiling lights of its room for the whole team — a dark room hides a player from eyes, a lit one shows them. The
    /// server keeps which rooms are switched (<see cref="Game.GameManager.UseServer"/>), every peer's lamps
    /// (<see cref="RoomLight"/>) and switches follow it. Its rocker is up while the lights are on; a small lamp in it glows
    /// while they are off, so the switch is found in the dark.
    /// </summary>
    public class LightSwitch : Interactable
    {
        [Tooltip("The rocker: tilted up while the room is lit, down while it is dark.")]
        public Transform Rocker;
        [Tooltip("The orientation lamp in the rocker: glows while the room is dark.")]
        public Renderer Led;


        /// <summary>The room whose lights it switches.</summary>
        public int Room { get; private set; } = -1;

        public override UseKind Kind => UseKind.LightSwitch;
        public override Vector3 Point => transform.position + transform.forward * 0.02f;
        public override float AimRadius => 0.3f;

        bool m_Dark;
        int m_Shown = -1;

        /// <summary>Sets it up for a room (<paramref name="dark"/>: the room's lamps start off).</summary>
        public void Setup(int index, int room, bool dark)
        {
            Room = room;
            m_Dark = dark;
            Configure(index);
        }

        /// <summary>Whether its room is lit now.</summary>
        public bool LightsOn => LightMath.SwitchedOn(m_Dark, World.IsOpened(OpenKeys.Lights(Room)));

        public override string Prompt(PlayerController player, out bool available)
        {
            available = true;
            return Loc.T(LightsOn ? "use.lights_off" : "use.lights_on");
        }

        void Update()
        {
            if (Index < 0) return;
            int on = LightsOn ? 1 : 0;
            if (on == m_Shown) return;
            if (m_Shown >= 0) Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.Click : null, Point, 0.6f, Audio.Sfx.Group.Items);
            m_Shown = on;
            if (Rocker != null) Rocker.localRotation = Quaternion.Euler(on == 1 ? -9f : 9f, 0f, 0f);
            if (Led != null) Led.enabled = on == 0;
        }
    }
}
