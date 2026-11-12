using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// The shared map, pings and marks (host + clients, <c>Tools/dev.sh run map 1</c>): the host walks through a few
        /// rooms, marks a robot, looks at it through a wall, pings a spot and opens the big map and the pause menu;
        /// a client stands still and must see what the host explored, the ping and the mark.
        /// </summary>
        IEnumerator MapScenario()
        {
            DevFlags.FreezeRobots = true;
            var gm = GameManager.Instance;
            var layout = ActiveLevel.Layout;
            Log($"house {layout.Mass} {layout.SizeX:0}x{layout.SizeZ:0} m, {layout.Rooms.Count} rooms x {layout.Floors} floors for {layout.Players} players, quota {gm.Quota}");
            float t0 = Time.time;
            bool expectClients = Me.IsServer && Arg("-autotest-linger") != null;
            while (expectClients && PlayerController.All.Count < 2 && Time.time - t0 < 25f) yield return null;
            yield return new WaitForSeconds(1f);
            Log($"players {PlayerController.All.Count}, quota now {gm.Quota} (team {gm.Run.Players})");

            if (!Me.IsServer)
            {
                // The client stands at the hatch and gets what the host sees.
                float before = gm.Exploration.RevealedShare(0);
                yield return Shot("client_minimap_start");
                yield return new WaitForSeconds(7f);
                Log($"client: explored {before:P0} -> {gm.Exploration.RevealedShare(0):P0} (host's rooms), pings {gm.Pings.Active.Count}, marked {Detector.All.Count(d => d.IsMarked)}");
                GameHUD.Instance.SetBigMap(true);
                yield return new WaitForSeconds(0.6f);
                yield return Shot("client_bigmap_shared");
                GameHUD.Instance.SetBigMap(false);
                yield return new WaitForSeconds(6f);
                yield break;
            }

            yield return Shot("1_minimap_start");
            float start = gm.Exploration.RevealedShare(0);

            // Walk through the nearest rooms of the ground floor (breadth first from the lobby).
            var rooms = new List<int>();
            var seen = new HashSet<int>();
            var queue = new Queue<int>();
            int first = layout.LobbyRoom;
            queue.Enqueue(first);
            seen.Add(first);
            while (queue.Count > 0 && rooms.Count < 6)
            {
                int c = queue.Dequeue();
                rooms.Add(c);
                foreach (int n in layout.Neighbours(c))
                    if (layout.Rooms[n].Floor == 0 && seen.Add(n)) queue.Enqueue(n);
            }
            Vector3 prev = layout.Lobby.Center;
            foreach (int c in rooms)
            {
                var centre = layout.Rooms[c].Center;
                var dir = Flat(centre - prev);
                Me.TeleportOwner(centre + Vector3.up * 0.05f, Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.forward));
                prev = centre;
                yield return new WaitForSeconds(0.5f);
            }
            float explored = gm.Exploration.RevealedShare(0);
            Log($"explored floor 1: {start:P0} -> {explored:P0} after {rooms.Count} rooms");
            if (explored <= start) Log("FAIL: walking revealed nothing");
            yield return Shot("2_minimap_explored");

            // Mark the nearest robot on the ground floor, then look at it through a wall.
            var robot = Detector.All.OfType<RobotController>().Where(r => LevelLayout.FloorOf(r.transform.position.y) == 0)
                .OrderBy(r => Vector3.Distance(r.transform.position, Me.transform.position)).FirstOrDefault();
            if (robot != null)
            {
                var view = ViewPoint(robot.transform.position, 5f);
                LookAtFrom(view, robot.Position);
                yield return new WaitForSeconds(0.4f);
                Me.Callout(true);
                yield return new WaitForSeconds(0.5f);
                Log($"marked: {robot.DisplayName} marked={robot.IsMarked}");
                if (!robot.IsMarked) Log("FAIL: the robot is not marked");
                yield return Shot("3_marked_in_view");
                // Into the next room, facing the robot: the wall is in between.
                // the room next door
                foreach (var other in layout.Rooms.Where(r => r.Floor == 0 && r != layout.RoomAt(robot.transform.position))
                                 .OrderBy(r => Vector3.Distance(r.Center, robot.transform.position)).Take(6))
                {
                        var spot = other.Center + Flat(robot.transform.position - other.Center).normalized * Mathf.Min(2f, Vector3.Distance(other.Center, robot.transform.position) * 0.3f);
                        if (layout.RoomAt(spot) != other) spot = other.Center;
                    if (!Physics.Linecast(spot + Vector3.up * 1.6f, robot.Position, Perception.SightMask, QueryTriggerInteraction.Ignore)) continue;
                    LookAtFrom(new Pose(spot + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(robot.Position - spot))), robot.Position);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("4_marked_through_wall");
                    break;
                }
            }
            else Log("no robot on the ground floor to mark");

            // Ping a spot in the lobby.
            var lobby = layout.Lobby.Center;
            LookAtFrom(new Pose(lobby + new Vector3(-3f, 0.05f, -3f), Quaternion.LookRotation(new Vector3(1f, 0f, 1f))), lobby + new Vector3(2f, 0f, 2f));
            yield return new WaitForSeconds(0.6f); // the callout cooldown
            Me.Callout(false);
            yield return new WaitForSeconds(0.4f);
            Log($"pings: {gm.Pings.Active.Count}");
            yield return Shot("5_ping");
            Me.TeleportOwner(lobby + new Vector3(3f, 0.05f, -3f), Quaternion.LookRotation(Vector3.back)); // the ping behind the back
            yield return new WaitForSeconds(0.3f);
            yield return Shot("6_ping_off_screen");

            GameHUD.Instance.SetBigMap(true);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("7_bigmap");
            GameHUD.Instance.SetBigMap(false);

            GameMenu.OpenPauseForTest();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("8_pause_controls");
            GameMenu.ClosePauseForTest();
            yield return new WaitForSeconds(4f);
        }
    }
}
