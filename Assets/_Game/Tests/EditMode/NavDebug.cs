using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Tests
{
    [Explicit("debug")]
    public class NavDebug
    {
        [TearDown]
        public void Cleanup()
        {
            NavMesh.RemoveAllNavMeshData();
            ActiveLevel.Clear();
        }

        [Test]
        public void TrimCase()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var l = LevelGenerator.Generate(5, 3);
            ActiveLevel.Load(l, art);
            var p = new Vector3(18.5f, 0f, 42f);
            for (int i = 0; i < l.Plan.Walls.Count; i++)
            {
                var w = l.Plan.Walls[i];
                if (w.Floor != 0) continue;
                var mid = (w.A + w.B) / 2f;
                if ((new Vector2(p.x, p.z) - mid).magnitude > 4f + w.Length / 2f) continue;
                Debug.Log($"[TRIM] wall {i} {w.A}->{w.B} rooms {w.RoomA}|{w.RoomB} ext {w.Exterior} shaped {w.Shaped} full {w.FullHeight} openings {string.Join(",", l.Plan.OpeningsOf(i).Select(o => o.Kind + "@" + o.At))}");
            }
            foreach (var f in ActiveLevel.Shell.Walls)
                if (f.Floor == 0 && (f.A - p).magnitude < 3.5f || (f.B - p).magnitude < 3.5f)
                    Debug.Log($"[TRIM] face {f.A}->{f.B} n {f.Normal} y {f.Y0}-{f.Y1} cornerA {f.CornerA} cornerB {f.CornerB}");
        }

        [Test]
        public void Timings()
        {
            LevelGenerator.Timing.Reset();
            int removed = 0, dropped = 0;
            for (int i = 0; i < 10; i++) { LevelGenerator.Generate(1000 + i * 7919, 3 + i % 3); removed += Furnisher.LastRemoved; dropped += Furnisher.LastDroppedItems; }
            Debug.Log($"[TIME] plan {LevelGenerator.Timing.Plan} check {LevelGenerator.Timing.Check} furnish {LevelGenerator.Timing.Furnish} walk {LevelGenerator.Timing.Walk} (grid {LevelGenerator.Timing.Grid} flood {LevelGenerator.Timing.Flood} reach {LevelGenerator.Timing.Reach} barrier {LevelGenerator.Timing.Barrier}, {LevelGenerator.Timing.Rounds} rounds, removed {removed}, dropped {dropped}) ms for 10 houses");
        }

        [Test]
        public void Walkability()
        {
            foreach (var seed in new[] { 777 })
            {
                var l = LevelGenerator.Generate(seed, 4);
                var perFloor = string.Join(",", Enumerable.Range(0, l.Floors).Select(f => l.ItemSpots.Count(p => LevelLayout.FloorOf(p.y) == f)));
                Debug.Log($"[WALK] seed {seed}: removed {Furnisher.LastRemoved} groups, dropped {Furnisher.LastDroppedItems} items, items per floor {perFloor}, props {l.Props.Count}; " + string.Join(" / ", Furnisher.LastNotes.Distinct()));
            }
        }

        [Test]
        public void Explain([Values(1, 1337)] int seed)
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var l = LevelGenerator.Generate(seed, 3);
            var root = ActiveLevel.Load(l, art);
            root.GetComponent<NavMeshSurface>().BuildNavMesh();
            NavMesh.SamplePosition(l.HatchPosition, out var h, 2f, NavMesh.AllAreas);
            bool Reach(Vector3 to, out string why)
            {
                why = "";
                if (!NavMesh.SamplePosition(to, out var b, 2f, NavMesh.AllAreas)) { why = "no navmesh near"; return false; }
                var path = new NavMeshPath();
                NavMesh.CalculatePath(h.position, b.position, NavMesh.AllAreas, path);
                why = $"{path.status} sampled {b.position}";
                return path.status == NavMeshPathStatus.PathComplete;
            }
            Physics.SyncTransforms();
            foreach (var s in l.Stairs) Debug.Log($"[NAV] stair floor {s.Floor} origin {s.Origin} corner {s.Corner} rooms {s.Room}->{s.RoomAbove} area {PlanGenerator.StairArea(s, 0f)}");
            if (seed == 42)
                for (float z = 3f; z < 40f; z += 0.5f)
                {
                    var at = new Vector3(11.75f, 0.5f, z);
                    var hits = Physics.OverlapSphere(at, 0.6f).Select(c => c.name + "@" + c.transform.position).ToArray();
                    bool nav = NavMesh.SamplePosition(at, out var hh, 0.6f, NavMesh.AllAreas);
                    Debug.Log($"[NAV] probe z {z}: nav {nav} {hh.position} {string.Join(", ", hits)}");
                }
            foreach (var d in l.Doorways)
            {
                if (d.Kind is not (EdgeType.Door or EdgeType.Open)) continue;
                var across = d.Rotation * Vector3.forward;
                var along = d.Rotation * Vector3.right;
                // pick the opening's own centre for open passages (whole wall)
                var pa = d.Position + across * 0.7f;
                var pb = d.Position - across * 0.7f;
                bool okA = NavMesh.SamplePosition(pa, out var ha, 0.5f, NavMesh.AllAreas), okB = NavMesh.SamplePosition(pb, out var hb, 0.5f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                if (okA && okB) NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
                float len = 0f;
                for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                if (okA && okB && path.status == NavMeshPathStatus.PathComplete && len < 4f) continue;
                var hits = Physics.OverlapBox(d.Position + Vector3.up * 1f, new Vector3(0.6f, 0.9f, 0.6f), d.Rotation).Select(c => c.name + "@" + c.transform.position + " " + c.bounds.size).ToArray();
                Debug.Log($"[NAV] BLOCKED {d.Kind} #{d.RoomA}|#{d.RoomB} at {d.Position} width {d.Width} navA {okA} navB {okB} {path.status} len {len:0.0}: {string.Join(", ", hits)}");
                foreach (var side in new[] { across, -across })
                    for (float k = 0.4f; k <= 2.01f; k += 0.4f)
                    {
                        var q = d.Position + side * k + Vector3.up * 0.1f;
                        bool s2 = NavMesh.SamplePosition(q, out var hq, 2f, NavMesh.AllAreas);
                        var near = Physics.OverlapBox(q + Vector3.up * 0.5f, new Vector3(0.15f, 0.4f, 0.15f)).Select(c => c.name + "@" + c.transform.position).ToArray();
                        var room = l.RoomAt(q);
                        Debug.Log($"[NAV]    {q}: room {(room != null ? room.Id + " " + room.Type : "none")} nav {s2} {hq.position} hits {string.Join(", ", near)}");
                    }
            }
            for (int cz = 22; cz >= 8; cz--)
            {
                var row = new System.Text.StringBuilder();
                for (int cx = 16; cx <= 32; cx++)
                {
                    var k = l.Plan.KindAt(0, cx, cz);
                    int id = l.Plan.IdAt(0, cx, cz);
                    row.Append(k == CellKind.Room ? $"{id,3}" : k == CellKind.Pocket ? $" p{id % 100,-2}" : "  .");
                }
                Debug.Log($"[NAV] z {cz * 0.5f,5}: {row}");
            }
            foreach (var rr in l.Rooms.Where(rr => rr.Floor == 0 && rr.Bounds.Overlaps(new Rect(8, 4, 8, 8)))) Debug.Log($"[NAV] room #{rr.Id} {rr.Type} {rr.Shape} {rr.Bounds}");
            // the plan's way from the lobby to every room the navmesh cannot reach: each step checked on the navmesh
            var parent = new System.Collections.Generic.Dictionary<int, (int from, string how, Vector3 at)>();
            var queue = new System.Collections.Generic.Queue<int>();
            parent[l.LobbyRoom] = (-1, "start", l.HatchPosition);
            queue.Enqueue(l.LobbyRoom);
            while (queue.Count > 0)
            {
                int r = queue.Dequeue();
                foreach (var d in l.Doorways)
                {
                    if (d.Kind is not (EdgeType.Door or EdgeType.Open)) continue;
                    int other = d.RoomA == r ? d.RoomB : d.RoomB == r ? d.RoomA : -1;
                    if (other < 0 || parent.ContainsKey(other)) continue;
                    parent[other] = (r, d.Kind.ToString(), d.Position);
                    queue.Enqueue(other);
                }
                foreach (var s in l.Stairs)
                {
                    int other = s.Room == r ? s.RoomAbove : s.RoomAbove == r ? s.Room : -1;
                    if (other < 0 || parent.ContainsKey(other)) continue;
                    parent[other] = (r, "stair", StairGeometry.Origin(s));
                    queue.Enqueue(other);
                }
            }
            foreach (var r in l.Rooms)
            {
                if (Reach(r.Center, out _)) continue;
                var chain = new System.Collections.Generic.List<string>();
                int k = r.Id;
                while (k >= 0 && parent.ContainsKey(k))
                {
                    var (from, how, at) = parent[k];
                    var rm = l.Rooms[k];
                    Reach(rm.Center, out var why);
                    chain.Add($"#{k} {rm.Type} c{rm.Center} {(why.StartsWith("PathComplete") ? "OK" : "NO")} <-{how} at {at}");
                    k = from;
                }
                Debug.Log($"[NAV] CHAIN to #{r.Id}: " + string.Join(" | ", chain));
                break;
            }
            // reachability picture: green reachable, red navmesh not reachable, over the plan
            var tex = new Texture2D(2, 2);
            tex.LoadImage(PlanPictures.Draw(l));
            int fw = tex.width / l.Floors;
            for (int f = 0; f < l.Floors; f++)
                for (float x = 0.125f; x < l.SizeX; x += 0.25f)
                    for (float z = 0.125f; z < l.SizeZ; z += 0.25f)
                    {
                        var at = new Vector3(x, LevelLayout.FloorY(f) + 0.1f, z);
                        if (!NavMesh.SamplePosition(at, out var hit, 0.3f, NavMesh.AllAreas) || LevelLayout.FloorOf(hit.position.y) != f) continue;
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(h.position, hit.position, NavMesh.AllAreas, path);
                        var c = path.status == NavMeshPathStatus.PathComplete ? new Color(0f, 0.7f, 0f) : new Color(1f, 0f, 0f);
                        int px = f * fw + 8 + Mathf.FloorToInt(x * 8), py = 8 + Mathf.FloorToInt(z * 8);
                        tex.SetPixel(px, py, c);
                    }
            tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "plans", $"nav_{seed}.png"), tex.EncodeToPNG());
            foreach (var r in l.Rooms)
            {
                if (true) break;
                if (Reach(r.Center, out var why)) continue;
                Debug.Log($"[NAV] seed {seed}: {r.Type} #{r.Id} floor {r.Floor} centre {r.Center} unreachable: {why}");
                foreach (var d in l.Doorways.Where(d => d.RoomA == r.Id || d.RoomB == r.Id))
                {
                    var across = d.Rotation * Vector3.forward;
                    Reach(d.Position + across * 0.7f, out var w1);
                    Reach(d.Position - across * 0.7f, out var w2);
                    Debug.Log($"[NAV]   {d.Kind} to #{(d.RoomA == r.Id ? d.RoomB : d.RoomA)} at {d.Position} width {d.Width}: +side {w1}; -side {w2}");
                    Physics.SyncTransforms();
                    foreach (var side in new[] { across, -across })
                    {
                        var at = d.Position + side * 0.7f + Vector3.up * 0.6f;
                        var hits = Physics.OverlapSphere(at, 0.45f).Select(c => c.name + "@" + c.transform.position + " " + c.bounds.size).ToArray();
                        Debug.Log($"[NAV]     at {at}: {string.Join(", ", hits)}");
                    }
                }
                foreach (var s in l.Stairs.Where(s => s.Room == r.Id || s.RoomAbove == r.Id))
                    Debug.Log($"[NAV]   stair {s.Floor} origin {s.Origin} corner {s.Corner}");
                foreach (var p in l.Props.Where(p => l.RoomAt(p.Position) == r))
                    Debug.Log($"[NAV]   prop {p.Kind} at {p.Position} yaw {p.RotationY}");
            }
        }
    }
}
