using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>The server's part of the doors: players' E, robots at them (<see cref="DoorState"/>), and the keys every peer moves the leaves by.</summary>
    public partial class GameManager
    {
        /// <summary>Server: a player opens or shuts a door.</summary>
        void ToggleDoorServer(PlayerController player, AutoDoor door)
        {
            if (Phase != RunPhase.Playing || door == null || door.Spot < 0 || door.Vault || door.Locked || AutoDoor.Automatic) return;
            if (door.ToggleServer(player.transform.position)) PublishDoor(door);
        }

        /// <summary>Server: robots open the doors they stand at, and those doors shut behind them.</summary>
        void UpdateDoorsServer()
        {
            if (AutoDoor.Automatic) return;
            foreach (var door in AutoDoor.All)
                if (door != null && door.Spot >= 0 && !door.Vault && door.StepServer(Time.deltaTime)) PublishDoor(door);
        }

        void PublishDoor(AutoDoor door)
        {
            m_Opened.Remove(OpenKeys.DoorSwung(door.Spot, false));
            m_Opened.Remove(OpenKeys.DoorSwung(door.Spot, true));
            if (door.State.Open) m_Opened.Add(OpenKeys.DoorSwung(door.Spot, door.OpenedWay < 0f));
            OpenedVersion++;
        }
    }
}
