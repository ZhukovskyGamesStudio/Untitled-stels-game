using System.Collections;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>Walks a path with the real controller: turns towards the next corner and moves forward.</summary>
        IEnumerator WalkPath(Vector3[] corners, float timeout, bool sprint = false)
        {
            float t0 = Time.time;
            int i = 1;
            while (i < corners.Length && Time.time - t0 < timeout)
            {
                var to = Flat(corners[i] - Me.transform.position);
                if (to.magnitude < 0.35f) { i++; continue; }
                float yaw = Vector3.SignedAngle(Flat(Me.transform.forward), to, Vector3.up);
                float px = Mathf.Clamp(yaw, -12f, 12f) / GameSettings.Sensitivity;
                PlayerController.Autopilot = new PlayerInputState { Move = Mathf.Abs(yaw) < 50f ? Vector2.up : Vector2.zero, Look = new Vector2(px, 0), Sprint = sprint };
                yield return null;
            }
            Release();
        }

        IEnumerator FloorsScenario()
        {
            var l = LevelGenerator.Current;
            Log($"building: {l.Width}x{l.Height} rooms, {l.Floors} floors, stairs={l.Stairs.Count}");
            var s = l.Stairs[0];
            float cx = (s.Cell.x + (s.Corner.x > 0 ? 1 : 0)) * LevelGenerator.Cell, cz = (s.Cell.y + (s.Corner.y > 0 ? 1 : 0)) * LevelGenerator.Cell;
            var corner = new Vector3(cx, LevelLayout.FloorY(s.Floor), cz);
            var room = l.CellCenter(s.Floor, s.Cell.x, s.Cell.y);

            // View of the staircase from the room.
            var view = room + (room - corner).normalized * 1.5f + Vector3.up * 0.05f;
            LookAtFrom(new Pose(view, Quaternion.LookRotation(Flat(corner - view))), corner + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("stairs_from_room");

            // Walk up with the real controller along the navmesh path.
            var target = l.CellCenter(s.Floor + 1, s.Cell.x, s.Cell.y);
            var path = new NavMeshPath();
            NavMesh.SamplePosition(view, out var a, 2f, NavMesh.AllAreas);
            NavMesh.SamplePosition(target, out var b, 2f, NavMesh.AllAreas);
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            Log($"path to floor {s.Floor + 1}: {path.status}, {path.corners.Length} corners");
            float startY = Me.transform.position.y, maxY = startY;
            bool landingShot = false;
            var walk = WalkPath(path.corners, 25f);
            while (walk.MoveNext())
            {
                maxY = Mathf.Max(maxY, Me.transform.position.y);
                if (!landingShot && Me.transform.position.y > startY + 1.6f) { landingShot = true; yield return Shot("on_landing"); }
                yield return walk.Current;
            }
            int floorNow = LevelLayout.FloorOf(Me.transform.position.y);
            Log($"climb: y {startY:0.00} -> {Me.transform.position.y:0.00}, floor {floorNow}, reached={floorNow == s.Floor + 1}");
            // Look back down the stairwell.
            LookAtFrom(new Pose(Me.transform.position, Quaternion.LookRotation(Flat(corner - Me.transform.position))), corner + Vector3.up * (LevelLayout.FloorY(s.Floor) - corner.y + 0.5f));
            yield return new WaitForSeconds(0.4f);
            yield return Shot("down_the_stairwell");

            // Walk back down.
            NavMesh.CalculatePath(b.position, a.position, NavMesh.AllAreas, path);
            yield return WalkPath(path.corners, 25f);
            Log($"descend: y now {Me.transform.position.y:0.00}, floor {LevelLayout.FloorOf(Me.transform.position.y)}");

            // Upper floor overview.
            var top = l.CellCenter(l.Floors - 1, 0, 0) + new Vector3(-3.5f, 0.05f, -3.5f);
            LookAtFrom(new Pose(top, Quaternion.LookRotation(new Vector3(1, 0, 1))), top + new Vector3(10, 1.2f, 10));
            yield return new WaitForSeconds(0.5f);
            yield return Shot("top_floor");

            // Patrol for a while: wheeled robots must stay on their floor, walkers may change floors.
            var start = new System.Collections.Generic.Dictionary<Enemies.RobotController, int>();
            var visited = new System.Collections.Generic.Dictionary<Enemies.RobotController, System.Collections.Generic.HashSet<int>>();
            foreach (var r in Enemies.RobotController.All)
            {
                start[r] = LevelLayout.FloorOf(r.transform.position.y);
                visited[r] = new System.Collections.Generic.HashSet<int> { start[r] };
            }
            float t = Time.time;
            while (Time.time - t < 40f)
            {
                foreach (var r in Enemies.RobotController.All) visited[r].Add(LevelLayout.FloorOf(r.transform.position.y));
                yield return new WaitForSeconds(0.5f);
            }
            int wheeledLeft = 0, walkersMoved = 0;
            foreach (var kv in visited)
            {
                bool moved = kv.Value.Count > 1;
                if (!kv.Key.CanUseStairs && moved) wheeledLeft++;
                if (kv.Key.CanUseStairs && moved) walkersMoved++;
                Log($"  {kv.Key.name} stairs={kv.Key.CanUseStairs} floors visited: {string.Join(",", kv.Value)}");
            }
            Log($"floor rules: wheeled robots that left their floor={wheeledLeft}, walkers that changed floor={walkersMoved}");
        }

        /// <summary>One screenshot of every room type present in the building.</summary>
        IEnumerator RoomsScenario()
        {
            var l = LevelGenerator.Current;
            var seen = new System.Collections.Generic.HashSet<RoomType>();
            for (int f = 0; f < l.Floors; f++)
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height; y++)
                    {
                        var type = l.Rooms[f, x, y];
                        if (!seen.Add(type)) continue;
                        var c = l.CellCenter(f, x, y);
                        // Stand near one corner and look across the room towards the opposite one.
                        int q = l.StairQuadrant(f, new Vector2Int(x, y));
                        var from = c + LevelGenerator.Quadrants[q == 3 ? 0 : 3] * 1.45f + Vector3.up * 0.05f;
                        var to = c - (from - c) + Vector3.up * 0.6f;
                        LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(to - from))), to);
                        yield return new WaitForSeconds(0.5f);
                        yield return Shot($"room_{type}");
                    }
            Log($"room types: {string.Join(", ", seen)}");
        }
    }
}
