using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// A report on how the built houses look in space, for whoever hunts 3D bugs: furniture standing or hanging over
    /// windows, rooms left empty, rooms without what makes them that room, the same piece many times in one room,
    /// wall faces and wall panels that overlap, slivers of panels. Writes Logs/space_audit.txt; AUDIT="seed:house,…"
    /// picks the houses, AUDITSTYLE the style asset whose architecture is planned (S06_Storybook).
    /// </summary>
    [Explicit("writes a report: Logs/space_audit.txt")]
    public class SpaceAudit
    {
        [Test]
        public void WriteReport()
        {
            var houses = new List<(int seed, int house)>();
            var wanted = System.Environment.GetEnvironmentVariable("AUDIT");
            if (!string.IsNullOrEmpty(wanted))
                foreach (var s in wanted.Split(',')) { var p = s.Split(':'); houses.Add((int.Parse(p[0]), p.Length > 1 ? int.Parse(p[1]) : 3)); }
            else houses.AddRange(new[] { (7, 4), (5, 5), (42, 3), (1, 0), (1337, 2), (99991, 4) });
            string styleName = System.Environment.GetEnvironmentVariable("AUDITSTYLE");
            if (string.IsNullOrEmpty(styleName)) styleName = "S06_Storybook";
            var style = AssetDatabase.LoadAssetAtPath<VisualStyle>($"Assets/_Game/Data/Styles/{styleName}.asset");
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var sb = new StringBuilder();
            var totals = new SortedDictionary<string, int>();
            void Count(string key, int n = 1) { totals.TryGetValue(key, out int v); totals[key] = v + n; }
            var density = new SortedDictionary<string, List<float>>();

            var heights = new SortedDictionary<PropKind, (float top, float bottom)>();
            try
            {
                foreach (var (seed, house) in houses)
                {
                    var l = LevelGenerator.Generate(seed, house);
                    var cut = Furnisher.LastCut.ToList();
                    ActiveLevel.Load(l, art);
                    var root = ActiveLevel.Root;
                    var shell = ActiveLevel.Shell;
                    sb.AppendLine($"=== house seed {seed} level {house}: {l.Rooms.Count} rooms, {l.Props.Count} props, {shell.Windows.Count} windows, {l.Floors} floors, walk check removed {Furnisher.LastRemoved} groups, vault room {l.VaultRoom}, hidden {l.HiddenRoom}");

                    // --- the placed props with their real bounds
                    var props = new List<(PropKind kind, Bounds b, PlanRoom room, Vector3 pos)>();
                    foreach (Transform child in root.transform)
                    {
                        if (!System.Enum.TryParse<PropKind>(child.name, out var kind)) continue;
                        var rs = child.GetComponentsInChildren<Renderer>();
                        if (rs.Length == 0) continue;
                        var b = rs[0].bounds;
                        foreach (var r in rs) b.Encapsulate(r.bounds);
                        props.Add((kind, b, l.Plan.RoomAt(child.position + Vector3.up * 0.3f) ?? l.Plan.RoomAt(b.center), child.position));
                    }

                    foreach (var p in props)
                    {
                        float h = p.b.max.y - LevelLayout.FloorY(LevelLayout.FloorOf(p.pos.y + 0.3f));
                        float low = p.b.min.y - LevelLayout.FloorY(LevelLayout.FloorOf(p.pos.y + 0.3f));
                        if (!heights.TryGetValue(p.kind, out var hh) || h > hh.top) heights[p.kind] = (h, low);
                    }
                    int lowCount = 0;
                    // --- 1. furniture over windows
                    foreach (var (pos, yaw) in shell.Windows)
                    {
                        var inward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                        var along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                        float sill = pos.y - LevelMetrics.WindowHeight / 2f, top = pos.y + LevelMetrics.WindowHeight / 2f;
                        foreach (var p in props)
                        {
                            var d = p.b.center - pos;
                            float depth = Vector3.Dot(d, inward);
                            float halfAlong = Mathf.Abs(along.x) > 0.5f ? p.b.extents.x : p.b.extents.z;
                            float halfDeep = Mathf.Abs(along.x) > 0.5f ? p.b.extents.z : p.b.extents.x;
                            if (depth - halfDeep > 0.75f || depth + halfDeep < -0.2f) continue;        // not at this wall
                            float overlap = LevelMetrics.WindowWidth / 2f + halfAlong - Mathf.Abs(Vector3.Dot(d, along));
                            if (overlap < 0.05f) continue;
                            float over = Mathf.Min(p.b.max.y, top) - Mathf.Max(p.b.min.y, sill);
                            if (over < 0.08f) continue;                                                // stays under the sill
                            if (!Furnisher.Tall.Contains(p.kind) && !Furnisher.OnWall.Contains(p.kind)) { lowCount++; continue; } // a desk, a sofa: fine
                            sb.AppendLine($"  WINDOW {p.kind} in {p.room?.Type} covers the window at {pos:0.0}: {overlap:0.00} m wide, {over:0.00} m high (top {p.b.max.y - (pos.y - LevelMetrics.WindowCenterY):0.00})");
                            Count($"window covered by {p.kind}");
                            Count("window covered (all)");
                        }
                    }

                    Count("low furniture under a window (fine)", lowCount);
                    // --- 2. rooms: density, signature pieces, repeats
                    foreach (var room in l.Rooms)
                    {
                        if (room.Area < 6f) continue;
                        var mine = props.Where(p => p.room == room).ToList();
                        float foot = mine.Sum(p => p.b.size.x * p.b.size.z);
                        float ratio = foot / room.Area;
                        if (!density.TryGetValue(room.Type.ToString(), out var list)) density[room.Type.ToString()] = list = new List<float>();
                        list.Add(ratio);
                        bool corridor = room.Type is RoomType.Corridor or RoomType.Hall;
                        if (!corridor && room.Id != l.HiddenRoom && (mine.Count <= 2 || ratio < 0.05f))
                        {
                            sb.AppendLine($"  EMPTY {room.Type} #{room.Id} floor {room.Floor} {room.Area:0} m² ({room.Bounds.width:0.0}×{room.Bounds.height:0.0}, {room.Shape}): {mine.Count} pieces, {ratio:P0} covered: {string.Join(", ", mine.Select(p => p.kind))} | doors {l.Doorways.Count(d => d.Floor == room.Floor && (l.Plan.RoomAt(d.Position + d.Rotation * Vector3.forward * 0.5f + Vector3.up * 0.3f) == room || l.Plan.RoomAt(d.Position - d.Rotation * Vector3.forward * 0.5f + Vector3.up * 0.3f) == room))} dark {room.Dark} cut {cut.Count(c => l.Plan.RoomAt(c.pos + Vector3.up * 0.3f) == room)} patrol {l.PatrolPoints.Count(q => l.Plan.RoomAt(q + Vector3.up * 0.3f) == room)}");
                            Count($"empty {room.Type}");
                        }
                        if (room.Type == RoomType.Meeting) Count("meeting rooms");
                        if (room.Type == RoomType.Meeting && !mine.Any(p => p.kind is PropKind.ConferenceTable or PropKind.MeetingTable or PropKind.RoundTable))
                        {
                            sb.AppendLine($"  NOTABLE Meeting #{room.Id} {room.Area:0} m² ({room.Bounds.width:0.0}×{room.Bounds.height:0.0}): {string.Join(", ", mine.Select(p => p.kind))}");
                            Count("meeting room without a table");
                        }
                        if (room.Type == RoomType.Restroom && !mine.Any(p => p.kind is PropKind.ToiletStall or PropKind.Urinal))
                        {
                            sb.AppendLine($"  NOTOILET Restroom #{room.Id} {room.Area:0} m²: {string.Join(", ", mine.Select(p => p.kind))}");
                            Count("restroom without a toilet");
                        }
                        if (room.Type == RoomType.Restroom && !mine.Any(p => p.kind is PropKind.MirrorSink or PropKind.DoubleSink or PropKind.WashBasin))
                            Count("restroom without a basin");
                        foreach (var g in mine.GroupBy(p => p.kind))
                        {
                            int limit = g.Key switch
                            {
                                PropKind.WaterCooler or PropKind.Printer or PropKind.Fridge or PropKind.CoffeeMachine or PropKind.JanitorCart or PropKind.Globe or
                                    PropKind.Safe or PropKind.Jukebox or PropKind.Copier or PropKind.Stove or PropKind.DefibCabinet or PropKind.WallClock or
                                    PropKind.CoatRack or PropKind.Shredder or PropKind.WeighingScale or PropKind.Lectern or PropKind.ChangingTable => 1,
                                PropKind.TrashBin or PropKind.SanitaryBin or PropKind.FireCabinet or PropKind.Sculpture or PropKind.HandDryer or PropKind.IvStand or
                                    PropKind.Wheelchair or PropKind.FloorLamp or PropKind.UmbrellaStand or PropKind.PunchingBag or PropKind.RobotArm or PropKind.MicStand => 2,
                                _ => 99,
                            };
                            if (room.Area > 60f) limit *= 2;
                            if (g.Count() > limit)
                            {
                                sb.AppendLine($"  REPEAT {g.Count()} × {g.Key} in {room.Type} #{room.Id} ({room.Area:0} m²)");
                                Count($"repeat {g.Key}");
                            }
                        }
                    }

                    // --- 3. wall faces that overlap each other (the style's panels and trims are laid twice there)
                    for (int i = 0; i < shell.Walls.Count; i++)
                        for (int j = i + 1; j < shell.Walls.Count; j++)
                        {
                            var a = shell.Walls[i];
                            var b = shell.Walls[j];
                            if (a.Floor != b.Floor || Vector3.Dot(a.Normal, b.Normal) < 0.99f) continue;
                            if (Mathf.Abs(Vector3.Dot(b.At(0f) - a.At(0f), a.Normal)) > 0.02f) continue;
                            if (a.Y1 <= b.Y0 + 0.01f || b.Y1 <= a.Y0 + 0.01f) continue;
                            float a0 = 0f, a1 = a.Length;
                            float b0 = Vector3.Dot(b.A - a.A, a.Along), b1 = Vector3.Dot(b.B - a.A, a.Along);
                            if (b0 > b1) (b0, b1) = (b1, b0);
                            float ov = Mathf.Min(a1, b1) - Mathf.Max(a0, b0);
                            if (ov < 0.03f) continue;
                            sb.AppendLine($"  FACES overlap {ov:0.00} m floor {a.Floor} n {a.Normal}: [{a.A:0.00} - {a.B:0.00} y {a.Y0:0.0}-{a.Y1:0.0}] and [{b.A:0.00} - {b.B:0.00} y {b.Y0:0.0}-{b.Y1:0.0}]");
                            Count("wall faces overlap");
                        }

                    for (int i = 0; i < l.Plan.Walls.Count; i++)
                        for (int j = i + 1; j < l.Plan.Walls.Count; j++)
                        {
                            var a = l.Plan.Walls[i];
                            var b = l.Plan.Walls[j];
                            if (a.Floor != b.Floor || Mathf.Abs(Vector2.Dot(a.Dir, b.Dir)) < 0.999f) continue;
                            var nn = new Vector2(-a.Dir.y, a.Dir.x);
                            if (Mathf.Abs(Vector2.Dot(b.A - a.A, nn)) > 0.02f) continue;
                            float b0 = Vector2.Dot(b.A - a.A, a.Dir), b1 = Vector2.Dot(b.B - a.A, a.Dir);
                            if (b0 > b1) (b0, b1) = (b1, b0);
                            float ov = Mathf.Min(a.Length, b1) - Mathf.Max(0f, b0);
                            if (ov < 0.03f) continue;
                            sb.AppendLine($"  PLANWALLS overlap {ov:0.00} floor {a.Floor}: [{a.A}-{a.B} shaped {a.Shaped} rail {a.Railing} rooms {a.RoomA}/{a.RoomB} ext {a.Exterior}] and [{b.A}-{b.B} shaped {b.Shaped} rail {b.Railing} rooms {b.RoomA}/{b.RoomB} ext {b.Exterior}]");
                            Count("plan walls overlap");
                        }
                    // --- 4. the style's decor: panels that overlap or are slivers
                    if (style != null)
                    {
                        var decor = ArchitecturePlanner.Plan(shell, l, style.Geometry);
                        var panels = decor.Where(p => p.Kind == DecorKind.Wainscot).ToList();
                        int slivers = panels.Count(p => p.Scale.x < 0.45f), wide = panels.Count(p => p.Scale.x > 1.6f);
                        foreach (var p in panels.Where(p => p.Scale.x < 0.45f).Take(6))
                            sb.AppendLine($"  SLIVER panel {p.Scale.x:0.00} m at {p.Position:0.0}");
                        Count("panel slivers (< 0.45 m)", slivers);
                        Count("panels wider than 1.6 m", wide);
                        Count("panels (all)", panels.Count);
                        int overlaps = 0;
                        var buckets = panels.GroupBy(p => (Mathf.RoundToInt(p.Position.x / 4f), Mathf.RoundToInt(p.Position.z / 4f), Mathf.RoundToInt(p.Position.y)));
                        foreach (var g in buckets)
                        {
                            var list = g.ToList();
                            for (int i = 0; i < list.Count; i++)
                                for (int j = i + 1; j < list.Count; j++)
                                {
                                    var a = list[i];
                                    var b = list[j];
                                    var na = a.Rotation * Vector3.forward;
                                    if (Vector3.Dot(na, b.Rotation * Vector3.forward) < 0.99f) continue;
                                    var d = b.Position - a.Position;
                                    if (Mathf.Abs(Vector3.Dot(d, na)) > 0.02f) continue;
                                    float ov = (a.Scale.x + b.Scale.x) / 2f - Mathf.Abs(Vector3.Dot(d, a.Rotation * Vector3.right));
                                    if (ov < 0.03f) continue;
                                    overlaps++;
                                    if (overlaps <= 6) sb.AppendLine($"  PANELS overlap {ov:0.00} m at {a.Position:0.0} ({a.Scale.x:0.00} and {b.Scale.x:0.00} wide)");
                                }
                        }
                        Count("panels overlap", overlaps);
                    }
                }
            }
            finally { ActiveLevel.Clear(); }

            sb.AppendLine("=== totals");
            foreach (var kv in totals) sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
            sb.AppendLine("=== floor covered by furniture, by room type (min / median / max, rooms)");
            foreach (var kv in density)
            {
                var v = kv.Value.OrderBy(x => x).ToList();
                sb.AppendLine($"  {kv.Key,-12} {v[0]:P0} / {v[v.Count / 2]:P0} / {v[v.Count - 1]:P0}  ({v.Count})");
            }
            sb.AppendLine("=== heights");
            foreach (var kv in heights.OrderBy(k => k.Value.top)) sb.AppendLine($"  H {kv.Key} {kv.Value.top:0.00} {kv.Value.bottom:0.00}");
            string path = Path.Combine(Application.dataPath, "..", "Logs", "space_audit.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[SpaceAudit] {path}");
        }
    }
}
