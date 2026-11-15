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
            // the first view again with one thing off at a time: what the frame's GPU time goes to
            Me.TeleportOwner(views[0].position, views[0].rotation);
            Me.Motor.SetPitch(0);
            yield return new WaitForSeconds(0.5f);
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
            foreach (var name in new[] { "S03_Heist", "S02_Scrapyard", "S15_Blueprint", "S11_Noir" })
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
