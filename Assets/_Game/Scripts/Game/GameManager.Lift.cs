using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The apartment block's lift on the server: a call button on a landing or a floor button in the cab sends it
    /// (<see cref="LiftRules.Request"/>: only while it stands open); a player standing in the doorway while its doors close
    /// holds the trip (<see cref="LiftRules.Hold"/>). The state (<see cref="Lift"/>) is replicated as it is; every peer moves
    /// the cab and the doors from it (<see cref="LiftCab"/>, <see cref="LiftDoor"/>). A new house parks it open on the ground floor.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Where the lift goes and since when (server; replicated by the network twin).</summary>
        public LiftState Lift { get; set; } = LiftState.ParkedAt(0);

        static BuildingSettings LiftTimes => Tuning.Level.Building;


        /// <summary>Server: someone pressed a call button (<paramref name="cab"/> false) or a floor button in the cab.</summary>
        void LiftServer(PlayerController player, int floor, bool cab)
        {
            if (Phase != RunPhase.Playing || ActiveLevel.Layout == null || !ActiveLevel.Layout.HasLift) return;
            if (floor < 0 || floor >= ActiveLevel.Layout.Floors) return;
            // a floor button only from inside the cab (the server checks where the player is)
            if (cab && (LiftCab.Current == null || !LiftCab.Current.Holds(player.transform.position))) return;
            Lift = LiftRules.Request(Lift, floor, Session.ServerTime, LiftTimes.LiftFloorTime, LiftTimes.LiftDoorTime);
        }

        /// <summary>Server, every frame: while its doors close, a player in the doorway holds the lift.</summary>
        void UpdateLift()
        {
            var b = LiftTimes;
            if (LiftRules.Phase(Lift, Session.ServerTime, b.LiftFloorTime, b.LiftDoorTime) != LiftPhase.Closing) return;
            foreach (var d in LiftDoor.All)
            {
                if (d.Floor != Lift.From) continue;
                foreach (var p in PlayerController.All)
                    if (d.InDoorway(p.transform.position)) // lying there too: the doors never shut on anyone
                    {
                        Lift = LiftRules.Hold(Lift, Session.ServerTime, b.LiftFloorTime, b.LiftDoorTime);
                        return;
                    }
            }
        }
    }
}
