using System.Collections;
using System.Linq;
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
                DevFlags.Autopilot = new PlayerInputState { Move = Mathf.Abs(yaw) < 50f ? Vector2.up : Vector2.zero, Look = new Vector2(px, 0), Sprint = sprint };
                yield return null;
            }
            Release();
        }

        IEnumerator FloorsScenario()
        {
            var l = ActiveLevel.Layout;
            Log($"building: {l.Width}x{l.Height} rooms, {l.Floors} floors, stairs={l.Stairs.Count}");
            var s = l.Stairs[0];
            float cx = (s.Cell.x + (s.Corner.x > 0 ? 1 : 0)) * LevelMetrics.Cell, cz = (s.Cell.y + (s.Corner.y > 0 ? 1 : 0)) * LevelMetrics.Cell;
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
            foreach (var r in Enemies.RobotController.Robots)
            {
                start[r] = LevelLayout.FloorOf(r.transform.position.y);
                visited[r] = new System.Collections.Generic.HashSet<int> { start[r] };
            }
            float t = Time.time;
            while (Time.time - t < 40f)
            {
                foreach (var r in Enemies.RobotController.Robots) visited[r].Add(LevelLayout.FloorOf(r.transform.position.y));
                yield return new WaitForSeconds(0.5f);
            }
            int wheeledLeft = 0, walkersMoved = 0;
            foreach (var kv in visited)
            {
                bool moved = kv.Value.Count > 1;
                if (!kv.Key.Traits.CanUseStairs && moved) wheeledLeft++;
                if (kv.Key.Traits.CanUseStairs && moved) walkersMoved++;
                Log($"  {kv.Key.name} stairs={kv.Key.Traits.CanUseStairs} floors visited: {string.Join(",", kv.Value)}");
            }
            Log($"floor rules: wheeled robots that left their floor={wheeledLeft}, walkers that changed floor={walkersMoved}");
        }

        /// <summary>Average FPS (uncapped) at a few viewpoints on a big building.</summary>
        IEnumerator PerfScenario()
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            var l = ActiveLevel.Layout;
            var all = ActiveLevel.Root.GetComponentsInChildren<Renderer>();
            int renderers = 0, staticBatched = 0;
            var byKind = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var r in all)
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                renderers++;
                if (r.isPartOfStaticBatch) staticBatched++;
                var top = r.transform;
                while (top.parent != null && top.parent != ActiveLevel.Root.transform) top = top.parent;
                var kind = System.Text.RegularExpressions.Regex.Replace(top.name, @"[_\d]+$|\(Clone\)", "");
                byKind[kind] = byKind.TryGetValue(kind, out var n) ? n + 1 : 1;
            }
            Log($"perf: {renderers} renderers drawn ({all.Length} in the level, {staticBatched} static-batched); style decor: {StyleDecor.Count} pieces in {StyleDecor.Renderers} renderers; " +
                $"furniture: {PropBatches.Merged} renderers merged into {PropBatches.Renderers}");
            Log("perf: drawn by object: " + string.Join(", ", byKind.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key} {kv.Value}")));
            var views = new[]
            {
                new Pose(l.HatchPosition + new Vector3(-3, 0.05f, -3), Quaternion.LookRotation(new Vector3(1, 0, 1))),
                new Pose(new Vector3(-9f, 0.05f, l.Height * 5f), Quaternion.LookRotation(Vector3.right)),
                new Pose(l.CellCenter(l.Floors - 1, l.Width / 2, l.Height / 2) + Vector3.up * 0.05f, Quaternion.LookRotation(Vector3.forward)),
            };
            float sum = 0, worst = float.MaxValue;
            for (int i = 0; i < views.Length; i++)
            {
                Me.TeleportOwner(views[i].position, views[i].rotation);
                Me.Motor.SetPitch(0);
                yield return new WaitForSeconds(0.5f);
                int frames = 0;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 3f) { frames++; yield return null; }
                float fps = frames / (Time.realtimeSinceStartup - t0);
                sum += fps;
                worst = Mathf.Min(worst, fps);
                Log($"perf view {i}: {fps:0} fps");
            }
            Log($"perf: building {l.Width}x{l.Height}x{l.Floors}, {renderers} renderers drawn, avg {sum / views.Length:0} fps, worst {worst:0} fps");
            Application.targetFrameRate = 144;
        }

        /// <summary>Standing players are blocked by a vent, crouching players crawl through.</summary>
        IEnumerator VentScenario()
        {
            var l = ActiveLevel.Layout;
            for (int x = 0; x < l.Width - 1; x++)
                for (int y = 0; y < l.Height; y++)
                {
                    if (l.EdgesX[0, x, y] != EdgeType.Vent) continue;
                    float wx = (x + 1) * LevelMetrics.Cell, vz = y * LevelMetrics.Cell + LevelMetrics.Cell * 0.5f;
                    var from = new Vector3(wx - 2.2f, 0.05f, vz);
                    LookAtFrom(new Pose(from, Quaternion.LookRotation(Vector3.right)), new Vector3(wx, 0.6f, vz));
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("vent");

                    Drive(Vector2.up);
                    yield return new WaitForSeconds(1.6f);
                    Release();
                    bool standingPassed = Me.transform.position.x > wx + 0.3f;
                    Me.TeleportOwner(from, Quaternion.LookRotation(Vector3.right));
                    Drive(Vector2.up, crouch: true);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("crawling");
                    yield return new WaitForSeconds(2.5f);
                    Release();
                    bool crouchPassed = Me.transform.position.x > wx + 0.3f;
                    var cc = Me.GetComponent<CharacterController>();
                    Log($"vent at x={wx} z={vz}: standing passed={standingPassed}, crouching passed={crouchPassed} pos={Me.transform.position} crouch={Me.Motor.IsCrouching} h={cc.height:0.00} r={cc.radius}");
                    yield break;
                }
            Log("no vent on the ground floor");
        }

        /// <summary>One screenshot of every room type present in the building.</summary>
        IEnumerator RoomsScenario()
        {
            var l = ActiveLevel.Layout;
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
                        var from = c + LevelMetrics.Quadrants[q == 3 ? 0 : 3] * 1.45f + Vector3.up * 0.05f;
                        var to = c - (from - c) + Vector3.up * 0.6f;
                        LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(to - from))), to);
                        yield return new WaitForSeconds(0.5f);
                        yield return Shot($"room_{type}");
                        Log($"light in {type}: {Enemies.Perception.Describe(Me.transform.position + Vector3.up * 1.26f)}");
                    }
            Log($"room types: {string.Join(", ", seen)}");

            // A dark room, then the lobby during an alarm.
            for (int f = 0; f < l.Floors; f++)
                for (int x = 0; x < l.Width; x++)
                    for (int y = 0; y < l.Height; y++)
                    {
                        if (!l.Dark[f, x, y]) continue;
                        var c = l.CellCenter(f, x, y);
                        var from = c + new Vector3(-1.5f, 0.05f, -1.5f);
                        LookAtFrom(new Pose(from, Quaternion.LookRotation(new Vector3(1, 0, 1))), c + new Vector3(4, 0.5f, 4));
                        yield return new WaitForSeconds(0.5f);
                        yield return Shot("dark_room");
                        f = l.Floors; x = l.Width; break;
                    }
            var hatch = l.HatchPosition;
            LookAtFrom(new Pose(hatch + new Vector3(-3f, 0.05f, -3f), Quaternion.LookRotation(new Vector3(1, 0, 1))), hatch + new Vector3(3, 1, 3));
            if (Me.IsServer) GameManager.Instance.RaiseAlarmServer(hatch + Vector3.forward * 80f, 1f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("alarm_lights");

            // A sliding door: closed from afar, open when close.
            var door = AutoDoor.All.Count > 0 ? AutoDoor.All[0] : null;
            if (door != null)
            {
                var dp = door.transform.position;
                var away = door.transform.forward * 4.5f;
                LookAtFrom(new Pose(dp + away + Vector3.up * 0.05f, Quaternion.LookRotation(-away)), dp + Vector3.up * 1.2f);
                yield return new WaitForSeconds(1f);
                bool closedFar = !door.IsOpen;
                yield return Shot("door_closed");
                LookAtFrom(new Pose(dp + away * 0.35f + Vector3.up * 0.05f, Quaternion.LookRotation(-away)), dp + Vector3.up * 1.2f);
                yield return new WaitForSeconds(1f);
                yield return Shot("door_open");
                Log($"auto door: closed when far={closedFar}, open when near={door.IsOpen}");
            }

            // The building from the street.
            var outside = new Vector3(-9f, 0.05f, l.Height * LevelMetrics.Cell * 0.5f);
            LookAtFrom(new Pose(outside, Quaternion.LookRotation(Vector3.right)), new Vector3(0, l.Floors * 1.5f, outside.z));
            yield return new WaitForSeconds(2f);
            yield return Shot("facade");
        }
    }
}
