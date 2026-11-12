using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        bool m_StyleArgApplied;

        /// <summary>F8 — next visual style (any build); -style &lt;name&gt; picks one at start.</summary>
        void Update()
        {
            if (!m_StyleArgApplied)
            {
                m_StyleArgApplied = true;
                var name = Arg("-style");
                if (!string.IsNullOrEmpty(name))
                {
                    var style = VisualStyles.Find(name);
                    if (style != null) VisualStyles.Apply(style);
                    else Debug.LogWarning($"[Style] no style '{name}'");
                }
            }
            if (UI.ControlsPanel.Rebinding || !Controls.Pressed(GameAction.Style)) return;
            int next = VisualStyles.NextIndex();
            if (next < 0) return;
            // In a session the whole team switches (GameManager replicates the choice); in the menu only this window.
            var gm = GameManager.Instance;
            if (gm != null) gm.RequestStyle(next);
            else VisualStyles.Apply(VisualStyles.At(next));
        }

        /// <summary>
        /// The same frozen moments of a house in every visual style: the lobby with a teammate, a guard noticing the
        /// player, a dark room with a lamp and the sweeper, loot up close, the gas outside the door. Screenshots are
        /// named &lt;pose&gt;__&lt;style asset&gt; (Tools/style_compare.py builds the comparison page from them).
        /// </summary>
        IEnumerator StylesScenario()
        {
            DevFlags.SkipGracePeriod = true;
            DevFlags.FreezeRobots = true;
            foreach (var r in RobotController.Robots.ToArray()) Session.Despawn(r.gameObject);
            var gm = GameManager.Instance;
            var layout = ActiveLevel.Layout;
            var styles = VisualStyles.All.Where(s => s != null).ToList();
            Log($"styles: {styles.Count} styles, run seed {gm.RunSeed}, house seed {layout.Seed}");
            if (styles.Count == 0) { Log("FAIL: no styles"); yield break; }

            var mate = Mannequin(1); // a teammate from QA

            // 1. Lobby: the hatch, the stash board, a teammate by the hatch.
            {
                var spawn = layout.PlayerSpawns[0];
                var hatch = layout.HatchPosition;
                var dir = Flat(hatch - spawn).sqrMagnitude > 0.01f ? Flat(hatch - spawn).normalized : Vector3.forward;
                var side = Vector3.Cross(Vector3.up, dir);
                var eye = hatch - dir * 4.2f + side * 0.8f;
                PlaceMate(mate, hatch + dir * 1.1f - side * 1.1f, eye, 1f);
                LookAtFrom(new Pose(eye + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(hatch + dir * 1.2f - eye))), hatch + dir * 1.2f + Vector3.up * 1.1f);
                yield return EveryStyle(styles, "1_lobby", false);
            }

            // 2. A guard in a lit room has half noticed the player; a teammate hides behind furniture.
            var room = LitRoom(layout);
            var (stand, look) = RoomView(layout, room);
            {
                var guardAt = Vector3.Lerp(stand, look, 0.42f); // close enough to see the style's own guard
                if (UnityEngine.AI.NavMesh.SamplePosition(guardAt, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) guardAt = hit.position;
                LookAtFrom(new Pose(stand, Quaternion.LookRotation(Flat(look - stand))), guardAt + Vector3.up * 1.1f);
                var guard = gm.SpawnRobotServer(RobotKind.Guard, guardAt, Quaternion.LookRotation(Flat(stand - guardAt))).GetComponent<RobotController>();
                var right = Vector3.Cross(Vector3.up, Flat(look - stand).normalized);
                PlaceMate(mate, Vector3.Lerp(stand, guardAt, 0.3f) + right * 1.4f, guardAt, 0.65f);
                yield return new WaitForSeconds(0.6f);
                DevFlags.FreezeRobots = false;
                DevFlags.HoldRobots = true;
                float t0 = Time.time;
                while (Time.time - t0 < 12f && guard.Suspicion < 0.55f) yield return null;
                Log($"guard: suspicion {guard.Suspicion:0.00} after {Time.time - t0:0.0} s, {Vector3.Distance(guard.transform.position, Me.transform.position):0.0} m");
                yield return EveryStyle(styles, "2_guard", false);
                DevFlags.FreezeRobots = true;
                DevFlags.HoldRobots = false;
                Session.Despawn(guard.gameObject);
            }

            // 3. A dark room lit by a desk lamp, the sweeper's floor lidar crossing it.
            {
                var lamp = layout.Props.Where(p => p.Kind == PropKind.DeskLamp).OrderByDescending(p => layout.IsDarkAt(p.Position) ? 1 : 0)
                    .ThenBy(p => LevelLayout.FloorOf(p.Position.y)).Select(p => (Vector3?)p.Position).FirstOrDefault();
                var target = lamp ?? room.Center;
                var pose = ViewPoint(target, 3.2f);
                LookAtFrom(pose, target + Vector3.up * 0.5f);
                var sweeperAt = Vector3.Lerp(pose.position, target, 0.55f) + Vector3.Cross(Vector3.up, pose.forward) * 1.2f;
                if (UnityEngine.AI.NavMesh.SamplePosition(sweeperAt, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas)) sweeperAt = hit.position;
                var sweeper = gm.SpawnRobotServer(RobotKind.Sweeper, sweeperAt, Quaternion.LookRotation(-Vector3.Cross(Vector3.up, pose.forward)));
                mate.SetActive(false);
                yield return new WaitForSeconds(0.8f);
                Log($"dark room: lamp {(lamp.HasValue ? "found" : "none")}, dark={layout.IsDarkAt(target)}");
                yield return EveryStyle(styles, "3_dark_room", false);
                Session.Despawn(sweeper);
            }

            // 4. A pile of loot in the lit room, a teammate crouching at it.
            {
                var c = room.Center;
                var pile = c + (stand - c) * 0.15f;
                if (UnityEngine.AI.NavMesh.SamplePosition(pile, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) pile = hit.position;
                var spawned = new List<WorldItem>();
                for (int i = 0; i < 5; i++)
                {
                    var off = Quaternion.Euler(0, i * 72f, 0) * new Vector3(0.45f + 0.1f * i, 0, 0);
                    spawned.Add(gm.SpawnItemServer((byte)(i % ItemCatalog.Shapes.Length), (byte)(i % ItemCatalog.Colors.Length), pile + off + Vector3.up * 0.3f, i * 40f));
                }
                var toward = Flat(stand - pile).normalized;
                var side = Vector3.Cross(Vector3.up, toward);
                var eye = pile + toward * 2.3f + side * 0.6f;
                LookAtFrom(new Pose(eye, Quaternion.LookRotation(Flat(pile - eye))), pile + Vector3.up * 0.2f);
                PlaceMate(mate, pile - toward * 1.0f + side * 0.3f, eye, 0.65f);
                yield return new WaitForSeconds(1.2f);
                yield return EveryStyle(styles, "4_loot", false);
                foreach (var w in spawned) if (w != null) Session.Despawn(w.gameObject);
            }

            // 7 (shot before the guard portrait). The team: four IT people of the four departments in the lobby, facing us.
            {
                mate.SetActive(false);
                // a ceiling lamp between the camera and the team lights their faces
                var c = layout.Lobby.Center;
                var lamps = LevelBuilder.LightSpots(layout, layout.Lobby).Select(p => new Vector3(p.x, c.y, p.y)).ToList();
                Vector3 lamp = lamps[0], dir = Vector3.forward;
                bool clear = false;
                foreach (var lampAt in lamps)
                    foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    {
                        if (clear) break;
                        var l = lampAt;
                        var teamAt = l + d * 0.9f;
                        var cam = l - d * 3.4f;
                        if (Physics.Linecast(cam + Vector3.up * 1.6f, teamAt + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.CheckCapsule(teamAt + Vector3.up * 0.4f, teamAt + Vector3.up * 1.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        lamp = l; dir = d; clear = true;
                    }
                var side = Vector3.Cross(Vector3.up, dir);
                var centre = lamp + dir * 0.9f;
                var eye = lamp - dir * 3.4f;
                var team = new List<GameObject>();
                for (int k = 0; k < 4; k++)
                {
                    var m = Mannequin(k);
                    PlaceMate(m, centre + side * ((k - 1.5f) * 0.9f), eye, 1f);
                    team.Add(m);
                }
                LookAtFrom(new Pose(eye + Vector3.up * 0.05f, Quaternion.LookRotation(dir)), centre + Vector3.up * 1.1f);
                Log($"team: four departments under a lamp {(clear ? "with a clear view" : "(no clear spot)")}");
                yield return new WaitForSeconds(0.5f);
                yield return EveryStyle(styles, "7_team", false);
                foreach (var m in team) Destroy(m);
                mate.SetActive(true);
            }

            // 6 (shot before the lasers). The style's own guard robot up close, standing under a ceiling lamp of the lobby.
            {
                mate.SetActive(false);
                var c = layout.Lobby.Center;
                var lamps = LevelBuilder.LightSpots(layout, layout.Lobby).Select(p => new Vector3(p.x, c.y, p.y)).ToList();
                Vector3 at = default, eye = default;
                bool found = false;
                foreach (var lampAt in lamps)
                    foreach (var dz in new[] { Vector3.back * 3f, Vector3.forward * 3f, Vector3.left * 3f, Vector3.right * 3f })
                    {
                        if (found) break;
                        var spot = lampAt;
                        var cam = spot + dz;
                        if (Physics.CheckCapsule(spot + Vector3.up * 0.3f, spot + Vector3.up * 1.8f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Linecast(cam + Vector3.up * 1.6f, spot + Vector3.up * 1.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        at = spot; eye = cam; found = true;
                    }
                if (!found) { at = lamps[0]; eye = at + Vector3.back * 3f; }
                if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
                DevFlags.FreezeRobots = true;
                var portrait = gm.SpawnRobotServer(RobotKind.Guard, at, Quaternion.LookRotation(Flat(eye - at)));
                LookAtFrom(new Pose(eye + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(at - eye))), at + Vector3.up * 1.2f);
                yield return new WaitForSeconds(0.8f);
                Log($"portrait: guard under a lamp {(found ? "with a clear view" : "(no clear spot)")}, {Vector3.Distance(eye, at):0.0} m");
                yield return EveryStyle(styles, "6_guard_portrait", false);
                Session.Despawn(portrait);
                mate.SetActive(true);
            }

            // 5. Lasers across a doorway, a wall camera watching (or the exterior door when the house has no lasers).
            if (layout.LaserSpots.Count > 0)
            {
                var spot = layout.LaserSpots[0];
                var across = spot.Rotation * Vector3.forward;
                var eye = spot.Position - Flat(across).normalized * 3.2f + Vector3.Cross(Vector3.up, across) * 0.7f;
                eye.y = Mathf.Floor((spot.Position.y + 0.5f) / LevelMetrics.FloorHeight) * LevelMetrics.FloorHeight + 0.05f;
                LookAtFrom(new Pose(eye, Quaternion.LookRotation(Flat(spot.Position - eye))), spot.Position + Vector3.up * 0.4f);
                PlaceMate(mate, spot.Position + Flat(across).normalized * 1.8f, eye, 1f);
                yield return new WaitForSeconds(1.2f);
                Log($"lasers: {spot.Kind} gate, {layout.LaserSpots.Count} in the house");
                yield return EveryStyle(styles, "5_lasers", false);
            }
            else
            {
                var exit = layout.Doorways.Where(d => d.Exterior).Select(d => (Doorway?)d).FirstOrDefault();
                var door = exit?.Position ?? layout.Lobby.Center;
                var outward = exit.HasValue ? exit.Value.Rotation * Vector3.forward : Vector3.back;
                if (exit.HasValue && layout.IsInside(door + outward * 1.5f)) outward = -outward;
                var at = door - outward * 1.7f + Vector3.Cross(Vector3.up, outward) * 0.5f;
                LookAtFrom(new Pose(at + Vector3.up * 0.05f, Quaternion.LookRotation(outward)), door + outward * 2f + Vector3.up * 1.2f);
                PlaceMate(mate, door + outward * 0.9f - Vector3.Cross(Vector3.up, outward) * 0.3f, at, 1f);
                yield return new WaitForSeconds(2.5f);
                yield return EveryStyle(styles, "5_doorway", false);
            }

            Destroy(mate);
            VisualStyles.Apply(styles[0]);
        }

        /// <summary>Freezes the moment and shoots it in every style.</summary>
        IEnumerator EveryStyle(List<VisualStyle> styles, string pose, bool outside)
        {
            Time.timeScale = 0f;
            foreach (var style in styles)
            {
                VisualStyles.Apply(style);
                VisualStyles.SnapFog(outside);
                DressMannequins();
                yield return null;
                yield return null;
                yield return Shot($"{pose}__{style.name}");
            }
            Time.timeScale = 1f;
            VisualStyles.Apply(styles[0]);
        }

        /// <summary>A copy of the player model dressed as the player of a slot (department, team colour) in the style.</summary>
        static GameObject Mannequin(int slot)
        {
            var m = Instantiate(Me.Model.gameObject);
            foreach (var r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            VisualStyles.SetVariant(m, slot);
            PlayerLooks.Paint(m.transform, slot);
            s_Mannequins.Add((m, slot));
            DressMannequins();
            return m;
        }

        static readonly List<(GameObject model, int slot)> s_Mannequins = new();

        /// <summary>
        /// Mannequins are bare copies of the player model (no PlayerController), so they show the style's rigged human
        /// (VisualStyles.PlayerRig) here, the way a player does: model hidden, the rig painted for the slot, standing idle
        /// (evaluated once: the shots are taken with the time stopped).
        /// </summary>
        static void DressMannequins()
        {
            s_Mannequins.RemoveAll(x => x.model == null);
            var prefab = VisualStyles.PlayerRig;
            foreach (var (m, slot) in s_Mannequins)
            {
                var old = m.transform.Find("StyleRig");
                if (old != null) DestroyImmediate(old.gameObject);
                foreach (var r in m.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = prefab != null;
                if (prefab == null) continue;
                var rig = Instantiate(prefab, m.transform);
                rig.name = "StyleRig";
                rig.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                PlayerLooks.Paint(rig.transform, slot);
                if (rig.TryGetComponent<HumanRig>(out var h) && h.Animator != null) h.Animator.Update(0f);
            }
        }

        static void PlaceMate(GameObject mate, Vector3 at, Vector3 lookAt, float height)
        {
            var floor = Mathf.Floor((at.y + 0.5f) / LevelMetrics.FloorHeight) * LevelMetrics.FloorHeight;
            at.y = floor;
            mate.SetActive(true);
            mate.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Flat(lookAt - at).sqrMagnitude > 0.01f ? Flat(lookAt - at) : Vector3.forward));
            mate.transform.localScale = new Vector3(1f, height, 1f);
        }

        /// <summary>A lit, furnished room near the start (offices first).</summary>
        static PlanRoom LitRoom(LevelLayout l)
        {
            PlanRoom best = l.Lobby;
            float bestScore = float.MinValue;
            foreach (var r in l.Rooms)
            {
                if (r.Floor != 0 || r.Dark || r.Flicker || r.Id == l.LobbyRoom) continue;
                if (r.Type is RoomType.Lobby or RoomType.Restroom or RoomType.Storage or RoomType.Corridor) continue;
                int props = l.Props.Count(p => l.RoomAt(p.Position) == r);
                float score = props * 2f - Vector3.Distance(r.Center, l.Lobby.Center) / 10f + (r.Type is RoomType.Office or RoomType.OpenSpace ? 4f : 0f);
                if (score > bestScore) { bestScore = score; best = r; }
            }
            return best;
        }

        /// <summary>A corner of the room with a clear view across it.</summary>
        static (Vector3 stand, Vector3 look) RoomView(LevelLayout l, PlanRoom room)
        {
            var c = room.Center;
            var b = room.Bounds;
            var corners = new[] { new Vector3(b.xMin - c.x, 0, b.yMin - c.z), new Vector3(b.xMax - c.x, 0, b.yMin - c.z),
                                  new Vector3(b.xMin - c.x, 0, b.yMax - c.z), new Vector3(b.xMax - c.x, 0, b.yMax - c.z) };
            for (int i = 0; i < corners.Length; i++) corners[i] *= 0.72f;
            foreach (var k in corners)
            {
                var stand = c + k + Vector3.up * 0.05f;
                if (l.RoomAt(stand) != room) continue;
                var head = stand + Vector3.up * 1.6f;
                var look = c - k;
                if (Physics.CheckSphere(head, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckCapsule(stand + Vector3.up * 0.4f, head, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(head, look + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                return (stand, look);
            }
            return (c + corners[0] + Vector3.up * 0.05f, c - corners[0]);
        }
    }
}
