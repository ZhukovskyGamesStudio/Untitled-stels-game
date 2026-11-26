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
            Log($"building: {l.Mass} {l.SizeX:0}x{l.SizeZ:0} m, {l.Rooms.Count} rooms, {l.Floors} floors, stairs={l.Stairs.Count}");
            var s = l.Stairs[0];
            var corner = StairGeometry.Origin(s);
            var (bottom, upstairs) = LevelLayout.StairEnds(s);
            var room = bottom + (bottom - corner).normalized * 1.0f;

            // View of the staircase from the room.
            var view = room + (room - corner).normalized * 1.5f + Vector3.up * 0.05f;
            LookAtFrom(new Pose(view, Quaternion.LookRotation(Flat(corner - view))), corner + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("stairs_from_room");
            // Every look of the staircase from a step further back and higher.
            // from beyond the open corner of the staircase (its own frame: u = x, v = z; the holder flips u when mirrored)
            var looks = Object.FindObjectsByType<StairLook>(FindObjectsSortMode.None).OrderBy(l => (l.transform.position - corner).sqrMagnitude).FirstOrDefault();
            var frame = looks != null ? looks.transform.Find("Looks") : null;
            var wide = frame != null ? frame.TransformPoint(new Vector3(6.2f, 0.05f, 6.2f)) : view + Vector3.up;
            var middle = frame != null ? frame.TransformPoint(new Vector3(2f, 1.6f, 2f)) : corner + Vector3.up * 1.3f;
            LookAtFrom(new Pose(wide, Quaternion.LookRotation(Flat(middle - wide))), middle);
            for (int i = 0; i < StairStyles.Count; i++)
            {
                DevFlags.ForcedStairStyle = i;
                yield return new WaitForSeconds(0.3f);
                yield return Shot($"stairs_look_{(StairStyle)i}");
            }
            DevFlags.ForcedStairStyle = -1;
            LookAtFrom(new Pose(view, Quaternion.LookRotation(Flat(corner - view))), corner + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.3f);

            // Walk up with the real controller along the navmesh path.
            var target = upstairs + (upstairs - new Vector3(corner.x, upstairs.y, corner.z)).normalized * 1.0f;
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
            var topRoom = l.Rooms.Where(r => r.Floor == l.Floors - 1).OrderByDescending(r => r.Area).First();
            var top = topRoom.Center + new Vector3(-2f, 0.05f, -2f);
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
                new Pose(new Vector3(-9f, 0.05f, l.SizeZ / 2f), Quaternion.LookRotation(Vector3.right)),
                new Pose(l.Rooms.Where(r => r.Floor == l.Floors - 1).OrderBy(r => (r.Center - new Vector3(l.SizeX / 2f, r.Center.y, l.SizeZ / 2f)).sqrMagnitude).First().Center + Vector3.up * 0.05f,
                    Quaternion.LookRotation(Vector3.forward)),
            };
            float sum = 0, worst = float.MaxValue;
            var profile = new PerfProfile();
            for (int i = 0; i < views.Length; i++)
            {
                Me.TeleportOwner(views[i].position, views[i].rotation);
                Me.Motor.SetPitch(0);
                yield return new WaitForSeconds(0.5f);
                int frames = 0;
                float t0 = Time.realtimeSinceStartup;
                profile.Begin();
                while (Time.realtimeSinceStartup - t0 < 3f) { frames++; profile.Sample(); yield return null; }
                float fps = frames / (Time.realtimeSinceStartup - t0);
                sum += fps;
                worst = Mathf.Min(worst, fps);
                Log($"perf view {i}: {fps:0} fps; {profile.Summary()}");
            }
            // playing: walking and turning through the house with the robots about
            Me.TeleportOwner(views[0].position, views[0].rotation);
            yield return new WaitForSeconds(0.5f);
            profile.Begin();
            float tw = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tw < 8f)
            {
                float k = Time.realtimeSinceStartup - tw;
                Drive(new Vector2(Mathf.Sin(k * 0.9f) * 0.6f, 1f), look: new Vector2(Mathf.Sign(Mathf.Sin(k * 0.5f)) * 3f, 0f));
                profile.Sample();
                yield return null;
            }
            Release();
            Log($"perf walking: {profile.Summary()}");
            foreach (var line in profile.Top(18)) Log("perf top: " + line);
            // the sweep: the corridors, halls and biggest rooms of every floor, eight ways each — where the frame is worst
            // (a house drops frames looking one way: down its long axis everything behind the walls is still in the frustum)
            var counters = new (string label, Unity.Profiling.ProfilerRecorder rec)[]
            {
                ("batches", Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count")),
                ("setpass", Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count")),
                ("tris", Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count")),
                ("shadow casters", Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Shadow Casters Count")),
            };
            var spots = l.Rooms.Where(r => r.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby).OrderByDescending(r => r.Area).Take(14)
                .Concat(l.Rooms.Where(r => r.Type is not (RoomType.Corridor or RoomType.Hall or RoomType.Lobby)).OrderByDescending(r => r.Area).Take(8)).ToList();
            var worstPose = views[0];
            float worstMs = 0f;
            var sweep = new System.Collections.Generic.List<(float ms, string line)>();
            foreach (var room in spots)
                for (int k = 0; k < 8; k++)
                {
                    var pose = new Pose(room.Center + Vector3.up * 0.05f, Quaternion.Euler(0f, k * 45f, 0f));
                    Me.TeleportOwner(pose.position, pose.rotation);
                    Me.Motor.SetPitch(0);
                    yield return new WaitForSeconds(0.3f);
                    int frames = 0;
                    float t0 = Time.realtimeSinceStartup;
                    profile.Begin();
                    while (Time.realtimeSinceStartup - t0 < 0.5f) { frames++; profile.Sample(); yield return null; }
                    float ms = (Time.realtimeSinceStartup - t0) * 1000f / Mathf.Max(1, frames);
                    string line = $"{ms:0.0} ms ({1000f / ms:0} fps) floor {room.Floor} room {room.Id} {room.Type} {room.Area:0} m2 yaw {k * 45}: {profile.Summary()}; " +
                        string.Join(", ", counters.Select(c => $"{c.label} {(c.rec.Valid ? c.rec.LastValue : -1)}"));
                    sweep.Add((ms, line));
                    if (ms > worstMs) { worstMs = ms; worstPose = pose; }
                }
            sweep.Sort((x, y) => y.ms.CompareTo(x.ms));
            for (int i = 0; i < sweep.Count; i++) if (i < 12 || i >= sweep.Count - 4 || i == sweep.Count / 2) Log($"perf sweep #{i + 1} of {sweep.Count}: {sweep[i].line}");
            // the worst view: what its frame goes to, then one thing off at a time
            Me.TeleportOwner(worstPose.position, worstPose.rotation);
            Me.Motor.SetPitch(0);
            yield return new WaitForSeconds(0.5f);
            using (var worstProfile = new PerfProfile())
            {
                worstProfile.Begin();
                float t2 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t2 < 3f) { worstProfile.Sample(); yield return null; }
                Log($"perf worst view: {worstProfile.Summary()}; " + string.Join(", ", counters.Select(c => $"{c.label} {(c.rec.Valid ? c.rec.LastValue : -1)}")));
                foreach (var line in worstProfile.Top(24)) Log("perf worst top: " + line);
            }
            // what the worst view draws, by the kind of thing: triangles and renderers in sight of a camera or a shadow map
            var drawn = new System.Collections.Generic.Dictionary<string, (long tris, int renderers)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff || !r.isVisible || !r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                long tris = 0;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) tris += filter.sharedMesh.GetIndexCount(i) / 3;
                var top = r.transform;
                while (top.parent != null && top.parent != ActiveLevel.Root.transform) top = top.parent;
                string kind = System.Text.RegularExpressions.Regex.Replace(top.name, @"[_\-\d]+$|\(Clone\)", "");
                drawn[kind] = drawn.TryGetValue(kind, out var was) ? (was.tris + tris, was.renderers + 1) : (tris, 1);
            }
            Log($"perf worst view draws {drawn.Values.Sum(v => v.tris) / 1000} k triangles in {drawn.Values.Sum(v => v.renderers)} renderers (once each; shadows and passes draw them again): " +
                string.Join(", ", drawn.OrderByDescending(kv => kv.Value.tris).Take(14).Select(kv => $"{kv.Key} {kv.Value.tris / 1000} k in {kv.Value.renderers}")));
            int lights = 0, shadowed = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.enabled && light.gameObject.activeInHierarchy && light.cullingMask != 0) { lights++; if (light.shadows != LightShadows.None) shadowed++; }
            Log($"perf worst view: {lights} lights on, {shadowed} with shadows; rooms not drawn: {RoomCulling.Hidden} of {RoomCulling.Rooms}");
            foreach (var c in counters) c.rec.Dispose();
            foreach (var (label, off) in PerfToggles())
            {
                var undo = off();
                yield return new WaitForSeconds(0.4f);
                profile.Begin();
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 2f) { profile.Sample(); yield return null; }
                undo?.Invoke();
                Log($"perf without {label}: {profile.Summary()}");
            }
            profile.Dispose();
            Log($"perf: building {l.Mass} {l.SizeX:0}x{l.SizeZ:0} m x {l.Floors} floors, {l.Rooms.Count} rooms, {renderers} renderers drawn, avg {sum / views.Length:0} fps, worst {worst:0} fps");
            Application.targetFrameRate = 144;
        }

        /// <summary>The things the perf scenario switches off one at a time (each returns how to switch it back on).</summary>
        static System.Collections.Generic.IEnumerable<(string, System.Func<System.Action>)> PerfToggles()
        {
            yield return ("nothing", () => null);
            yield return ("room culling (everything drawn)", () => { RoomCulling.Enabled = false; return () => RoomCulling.Enabled = true; });
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            var field = typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var data = asset != null && field != null ? field.GetValue(asset) as UnityEngine.Rendering.Universal.ScriptableRendererData[] : null;
            if (data != null && data.Length > 0)
                foreach (var f in data[0].rendererFeatures)
                {
                    if (f == null || !f.isActive) continue;
                    var feature = f;
                    yield return (feature.name, () => { feature.SetActive(false); return () => feature.SetActive(true); });
                }
            yield return ("post-processing", () =>
            {
                var cam = Camera.main != null ? Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
                if (cam == null) return null;
                cam.renderPostProcessing = false;
                return () => cam.renderPostProcessing = true;
            });
            System.Func<System.Action> Lights(bool directional, LightShadows to) => () =>
            {
                var changed = new System.Collections.Generic.List<(Light, LightShadows)>();
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if ((l.type == LightType.Directional) == directional && l.shadows != LightShadows.None && l.shadows != to) { changed.Add((l, l.shadows)); l.shadows = to; }
                return () => { foreach (var (l, sh) in changed) if (l != null) l.shadows = sh; };
            };
            yield return ("soft shadows of lamps (hard)", Lights(false, LightShadows.Hard));
            yield return ("shadows of lamps", Lights(false, LightShadows.None));
            yield return ("shadows of the moon", Lights(true, LightShadows.None));
            yield return ("lamps", () =>
            {
                var off = new System.Collections.Generic.List<Light>();
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.enabled && l.type != LightType.Directional) { off.Add(l); l.enabled = false; }
                return () => { foreach (var l in off) if (l != null) l.enabled = true; };
            });
        }

        /// <summary>
        /// A vent under the ceiling: from the floor, jump onto the desk under it (holding jump and forward),
        /// then crouch and walk on — into the duct, through the wall and down the crates on the other side.
        /// </summary>
        IEnumerator HighVentClimb(LevelLayout l)
        {
            foreach (var d in l.Doorways)
            {
                if (!d.High) continue;
                var across = d.Rotation * Vector3.forward;
                var from = d.Position - across * 2.3f + Vector3.up * 0.05f;
                if (l.RoomAt(from) == null) { across = -across; from = d.Position - across * 2.3f + Vector3.up * 0.05f; }
                LookAtFrom(new Pose(from - across * 1.2f, Quaternion.LookRotation(across)), d.Position + Vector3.up * 1.4f);
                yield return new WaitForSeconds(0.6f);
                yield return Shot("high_vent");
                Me.TeleportOwner(from, Quaternion.LookRotation(across));
                Me.Motor.SetPitch(10f);
                yield return new WaitForSeconds(0.3f);
                float t0 = Time.time, top = 0f;
                while (Time.time - t0 < 2.2f)
                {
                    DevFlags.Autopilot = new PlayerInputState { Move = Vector2.up * 0.6f, Jump = true };
                    top = Mathf.Max(top, Me.transform.position.y - d.Position.y);
                    yield return null;
                }
                float onDesk = Me.transform.position.y - d.Position.y;
                yield return Shot("high_vent_on_desk");
                Drive(Vector2.up * 0.7f, crouch: true);
                yield return new WaitForSeconds(0.6f);
                yield return Shot("high_vent_in_duct");
                yield return new WaitForSeconds(3.4f);
                Release();
                float beyond = Vector3.Dot(Me.transform.position - d.Position, across);
                Log($"high vent at {d.Position}: on the desk y={onDesk:0.00} (highest {top:0.00}), through={beyond > 0.3f} ({beyond:0.0} m past the wall), " +
                    $"y now {Me.transform.position.y - d.Position.y:0.00}");
                // looking back at the hole under the ceiling and the desk under it
                LookAtFrom(new Pose(d.Position + across * 3.2f + Vector3.up * 0.05f, Quaternion.LookRotation(-across)), d.Position + Vector3.up * 1.2f);
                yield return new WaitForSeconds(0.5f);
                yield return Shot("high_vent_from_the_other_side");
                yield break;
            }
            Log("no high vent in this house");
        }

        /// <summary>Standing players are blocked by a vent, crouching players crawl through.</summary>
        IEnumerator VentScenario()
        {
            var l = ActiveLevel.Layout;
            yield return HighVentClimb(l);
            foreach (var d in l.Doorways)
            {
                if (d.Kind != EdgeType.Vent || d.High || d.Floor != 0) continue;
                var across = d.Rotation * Vector3.forward;
                var from = d.Position - across * 2.2f + Vector3.up * 0.05f;
                if (l.RoomAt(from) == null) { across = -across; from = d.Position - across * 2.2f + Vector3.up * 0.05f; }
                LookAtFrom(new Pose(from, Quaternion.LookRotation(across)), d.Position + Vector3.up * 0.6f);
                yield return new WaitForSeconds(0.5f);
                yield return Shot("vent");

                Drive(Vector2.up);
                yield return new WaitForSeconds(1.6f);
                Release();
                bool standingPassed = Vector3.Dot(Me.transform.position - d.Position, across) > 0.3f;
                Me.TeleportOwner(from, Quaternion.LookRotation(across));
                Drive(Vector2.up, crouch: true);
                yield return new WaitForSeconds(0.5f);
                yield return Shot("crawling");
                yield return new WaitForSeconds(2.5f);
                Release();
                bool crouchPassed = Vector3.Dot(Me.transform.position - d.Position, across) > 0.3f;
                var cc = Me.GetComponent<CharacterController>();
                Log($"vent at {d.Position}: standing passed={standingPassed}, crouching passed={crouchPassed} pos={Me.transform.position} crouch={Me.Motor.IsCrouching} h={cc.height:0.00} r={cc.radius}");
                yield break;
            }
            Log("no vent on the ground floor");
        }

        /// <summary>A view across a room: from near one of its corners towards the opposite side.</summary>
        static (Vector3 from, Vector3 to) ViewAcross(LevelLayout l, PlanRoom r)
        {
            var c = r.Center;
            var b = r.Bounds;
            foreach (var k in new[] { new Vector2(b.xMin, b.yMin), new Vector2(b.xMax, b.yMax), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMin) })
            {
                var corner = new Vector3(k.x, c.y, k.y);
                var from = Vector3.Lerp(c, corner, 0.7f) + Vector3.up * 0.05f;
                if (l.RoomAt(from) != r || l.NearStair(r.Floor, from, 0.5f)) continue;
                return (from, c - (from - c) + Vector3.up * 0.6f);
            }
            return (c + Vector3.up * 0.05f, c + Vector3.forward * 4f + Vector3.up * 0.6f);
        }

        /// <summary>
        /// Close-ups of where doorways meet their walls (doors and open passages): from both sides at an angle, at the joint
        /// of the trim with the wall finish, and up at the head — the style's decor must not stick out at the joint.
        /// </summary>
        IEnumerator DoorsScenario()
        {
            var l = ActiveLevel.Layout;
            DevFlags.FreezeRobots = true;
            // two doors, two open passages and a squeeze slot, in the default style: every angle
            var picked = new System.Collections.Generic.List<(Doorway d, string tag)>();
            int doors = 0, open = 0, squeezes = 0, vents = 0;
            foreach (var d in l.Doorways)
            {
                if (d.Exterior || d.High) continue;
                if (d.Kind == EdgeType.Door && doors < 2) picked.Add((d, $"door{++doors}"));
                else if (d.Kind == EdgeType.Open && open < 2) picked.Add((d, $"open{++open}"));
                else if (d.Kind == EdgeType.Squeeze && squeezes < 1) picked.Add((d, $"squeeze{++squeezes}"));
                else if (d.Kind == EdgeType.Vent && vents < 1) picked.Add((d, $"vent{++vents}"));
            }
            var start = VisualStyles.Current;
            foreach (var (d, tag) in picked)
                yield return ShootDoorway(d, $"{start?.name}_{tag}", true);
            // the ink outlines by channel over a door's head: a joint that looks seamless can still be inked (a chamfer, a
            // smoothed normal, a gap in the depth); "normalink" exaggerates the creases
            var ink = Shader.GetGlobalVector("_StyOutline");
            foreach (var (d, tag) in picked)
            {
                if (!tag.StartsWith("door")) continue;
                var across = d.Rotation * Vector3.forward;
                var near = d.Position + across * 1.3f + d.Rotation * Vector3.right * 0.3f;
                LookAtFrom(new Pose(near + Vector3.up * 0.05f, Quaternion.LookRotation(-across)), d.Position + d.Rotation * Vector3.right * 0.25f + Vector3.up * 2.35f);
                foreach (var (name, v) in new[] { ("noink", new Vector4(0f, ink.y, ink.z, ink.w)), ("depthink", new Vector4(1f, ink.y, 8f, 0f)), ("normalink", new Vector4(1f, ink.y, 0f, 8f)) })
                {
                    Shader.SetGlobalVector("_StyOutline", v);
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot($"{tag}_debug_{name}");
                }
                Shader.SetGlobalVector("_StyOutline", ink);
                break;
            }
            // the other kinds of door trim (pilasters, casings with panels or a cornice): the same doorways from the front
            foreach (var name in new[] { "S16_Derelict" })
            {
                var style = VisualStyles.Find(name);
                if (style == null) continue;
                VisualStyles.Apply(style);
                yield return new WaitForSeconds(0.6f);
                foreach (var (d, tag) in picked)
                    if (!tag.StartsWith("squeeze") && !tag.StartsWith("vent") && (tag.EndsWith("1") || style.Geometry.Doors != Configs.DoorTrim.None))
                        yield return ShootDoorway(d, $"{name}_{tag}", false);
            }
            if (start != null) VisualStyles.Apply(start);
            Log($"doors: {doors} doors, {open} open passages, {squeezes} squeezes shot (style {start?.name} + 4 more)");
        }

        /// <summary>A doorway's joints with its wall: from both sides at the jamb (all), straight on, and up at the wall over its head.</summary>
        IEnumerator ShootDoorway(Doorway d, string tag, bool all)
        {
            var along = d.Rotation * Vector3.right;
            var across = d.Rotation * Vector3.forward;
            float half = LevelMetrics.ClearWidth(d.Width) / 2f;
            var joint = d.Position + along * half;
            if (all)
                foreach (float side in new[] { 1f, -1f })
                {
                    // standing in the room, off to one side, looking at the jamb where the trim meets the wall
                    var stand = joint + across * (side * 1.9f) + along * 1.1f;
                    LookAtFrom(new Pose(stand + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(joint - stand))), joint + Vector3.up * 1.1f);
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot($"{tag}_{(side > 0 ? "a" : "b")}_jamb");
                }
            var front = d.Position + across * 2.6f;
            LookAtFrom(new Pose(front + Vector3.up * 0.05f, Quaternion.LookRotation(-across)), d.Position + Vector3.up * 1.3f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot($"{tag}_front");
            // close up at the wall over the head of the opening, both faces: where the trim's top meets the wall above it
            foreach (float side in all ? new[] { 1f, -1f } : new[] { 1f })
            {
                var near = d.Position + across * (side * 1.3f) + along * 0.3f;
                LookAtFrom(new Pose(near + Vector3.up * 0.05f, Quaternion.LookRotation(-across * side)), d.Position + along * (half * 0.6f) + Vector3.up * 2.35f);
                yield return new WaitForSeconds(0.4f);
                yield return Shot($"{tag}_{(side > 0 ? "a" : "b")}_top");
            }
        }

        /// <summary>
        /// A staircase alone in the open (the yard, no fog, the moon and a lamp), in every look, from every side, from above
        /// and from under the upper flight: everything that is drawn and should not be (a door to nowhere, a hole, a stray
        /// panel) shows when nothing stands around it.
        /// </summary>
        IEnumerator StairsScenario()
        {
            DevFlags.FreezeRobots = true;
            Me.CheatServer(Cheat.Invulnerable, true); // the gas outside
            var l = ActiveLevel.Layout;
            var at = new Vector3(-14f, 0f, l.SizeZ / 2f);
            var stairs = Instantiate(GameManager.Instance.Art.Stairs, at, Quaternion.identity);
            stairs.GetComponent<StairLook>().Configure(l.Seed);
            VisualStyles.Dress(stairs);
            var lamp = Instantiate(GameManager.Instance.Art.FloorLamp, at + new Vector3(5.5f, 0f, 5.5f), Quaternion.identity);
            RenderSettings.fog = false;
            var frame = stairs.transform.Find("Looks");
            Vector3 P(float u, float y, float v) => frame != null ? frame.TransformPoint(new Vector3(u, y, v)) : at + new Vector3(u, y, v);
            var mid = P(2f, 1.6f, 2f);
            var views = new (string name, Vector3 from, Vector3 to)[]
            {
                ("front", P(2f, 0.05f, -4.5f), mid), ("back", P(2f, 0.05f, 8.5f), mid),
                ("left", P(-4.5f, 0.05f, 2f), mid), ("right", P(8.5f, 0.05f, 2f), mid),
                ("corner", P(7f, 0.05f, -3f), mid), ("under", P(5.6f, 0.05f, 3f), P(3f, 1.2f, 3f)),
                ("above", P(2f, 3.6f, -2.5f), P(2f, 1.8f, 2f)),
            };
            for (int look = 0; look < StairStyles.Count; look++)
            {
                DevFlags.ForcedStairStyle = look;
                foreach (var (name, from, to) in views)
                {
                    RenderSettings.fog = false;
                    LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(to - from).sqrMagnitude > 1e-4f ? Flat(to - from) : Vector3.forward)), to);
                    yield return new WaitForSeconds(0.35f);
                    yield return Shot($"{(StairStyle)look}_{name}");
                }
            }
            DevFlags.ForcedStairStyle = -1;
            Log($"stairs: {StairStyles.Count} looks × {views.Length} views (style {VisualStyles.Current?.name})");
            Destroy(stairs);
            Destroy(lamp);
        }

        /// <summary>One screenshot of every room type and every room shape present in the building; atriums from below and above.</summary>
        IEnumerator RoomsScenario()
        {
            var l = ActiveLevel.Layout;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var r in l.Rooms)
            {
                string key = r.Shape is RoomShape.Rotunda or RoomShape.Octagon or RoomShape.L or RoomShape.U ? $"{r.Type}_{r.Shape}" : r.Type.ToString();
                bool atrium = r.Levels > 1;
                if (!seen.Add(key) && !atrium) continue;
                var (from, to) = ViewAcross(l, r);
                LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(to - from))), to + (atrium ? Vector3.up * 3f : Vector3.zero));
                yield return new WaitForSeconds(0.5f);
                yield return Shot($"room_{key}{(atrium ? $"_x{r.Levels}" : "")}");
                Log($"light in {key}: {Enemies.Perception.Describe(Me.transform.position + Vector3.up * 1.26f)}");
                if (!atrium) continue;
                // the atrium from a gallery above: look down into it
                foreach (var g in l.Rooms)
                {
                    if (g.Floor != r.Floor + 1) continue;
                    var dir = Flat(r.Center - g.Center);
                    if (dir.sqrMagnitude < 0.01f) continue;
                    // walk from the gallery room's middle towards the atrium while still on its floor
                    var gp = g.Center;
                    for (float k = 0f; k < 30f; k += 0.5f)
                    {
                        var next = g.Center + dir.normalized * k;
                        if (l.RoomAt(next) != g) break;
                        gp = next;
                    }
                    if ((Flat(gp - r.Center)).magnitude > Mathf.Max(r.Bounds.width, r.Bounds.height)) continue;
                    gp -= dir.normalized * 0.8f;
                    LookAtFrom(new Pose(gp + Vector3.up * 0.05f, Quaternion.LookRotation(dir)), r.Center + Vector3.up * 0.5f);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot($"atrium_from_above_{r.Type}");
                    break;
                }
            }
            Log($"rooms: {string.Join(", ", seen)}");

            // A dark room, then the lobby during an alarm.
            foreach (var r in l.Rooms)
            {
                if (!r.Dark) continue;
                var (from, to) = ViewAcross(l, r);
                LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(to - from))), to);
                yield return new WaitForSeconds(0.5f);
                yield return Shot("dark_room");
                break;
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
            var outside = new Vector3(-9f, 0.05f, l.SizeZ * 0.5f);
            LookAtFrom(new Pose(outside, Quaternion.LookRotation(Vector3.right)), new Vector3(0, l.Floors * 1.5f, outside.z));
            yield return new WaitForSeconds(2f);
            yield return Shot("facade");
        }
    }
}
