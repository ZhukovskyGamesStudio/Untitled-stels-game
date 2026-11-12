using System.IO;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Pictures of floor plans for the people (and agents) working on the generator: <c>Tools/dev.sh plans</c> writes
    /// Logs/plans/plan_&lt;seed&gt;_&lt;house&gt;.png — all floors side by side: rooms coloured by type (darker when dark),
    /// pockets grey, atrium voids dark blue, walls black, railings blue, doors green, open passages light, vents orange,
    /// slots violet, stairs yellow, furniture as dots, robots red, the hatch white.
    /// </summary>
    [Explicit("writes pictures: Tools/dev.sh plans")]
    public class PlanPictures
    {
        const int Px = 8; // pixels per metre

        [Test]
        public void WritePlans()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "plans");
            Directory.CreateDirectory(dir);
            // PLANS="seed:house,seed:house" picks the houses (Tools/dev.sh plans passes it through)
            var wanted = System.Environment.GetEnvironmentVariable("PLANS");
            var list = new System.Collections.Generic.List<(int seed, int house)>();
            if (!string.IsNullOrEmpty(wanted))
                foreach (var s in wanted.Split(','))
                {
                    var p = s.Split(':');
                    list.Add((int.Parse(p[0]), p.Length > 1 ? int.Parse(p[1]) : 3));
                }
            else
            {
                int[] seeds = { 1, 7, 42, 1337, 99991, 2024, 555, 31337 };
                for (int i = 0; i < seeds.Length; i++) list.Add((seeds[i], i % 6));
            }
            foreach (var (seed, house) in list)
                File.WriteAllBytes(Path.Combine(dir, $"plan_{seed}_{house}.png"), Draw(LevelGenerator.Generate(seed, house)));
        }

        static Color TypeColor(RoomType t) => t switch
        {
            RoomType.Lobby => new Color(0.55f, 0.85f, 0.6f),
            RoomType.OpenSpace => new Color(0.6f, 0.75f, 0.95f),
            RoomType.Office => new Color(0.75f, 0.7f, 0.95f),
            RoomType.Meeting => new Color(0.95f, 0.75f, 0.55f),
            RoomType.Kitchen => new Color(0.95f, 0.9f, 0.55f),
            RoomType.Server => new Color(0.5f, 0.8f, 0.85f),
            RoomType.Storage => new Color(0.8f, 0.7f, 0.55f),
            RoomType.Restroom => new Color(0.75f, 0.9f, 0.95f),
            RoomType.Lounge => new Color(0.95f, 0.65f, 0.75f),
            RoomType.Corridor => new Color(0.9f, 0.9f, 0.88f),
            _ => new Color(0.85f, 0.85f, 0.7f),
        };

        public static byte[] Draw(LevelLayout l)
        {
            var plan = l.Plan;
            int fw = Mathf.CeilToInt(plan.SizeX * Px) + 2 * Px, fh = Mathf.CeilToInt(plan.SizeZ * Px) + 2 * Px;
            var tex = new Texture2D(fw * plan.Floors, fh, TextureFormat.RGB24, false);
            var px = new Color[tex.width * tex.height];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            void Put(int f, float x, float z, Color c)
            {
                int ix = f * fw + Px + Mathf.FloorToInt(x * Px), iy = Px + Mathf.FloorToInt(z * Px);
                if (ix < f * fw || ix >= (f + 1) * fw || iy < 0 || iy >= fh) return;
                px[iy * tex.width + ix] = c;
            }
            void Line(int f, Vector2 a, Vector2 b, Color c, float width)
            {
                float len = (b - a).magnitude;
                int steps = Mathf.Max(1, Mathf.CeilToInt(len * Px * 2));
                var n = len > 0 ? new Vector2(-(b - a).y, (b - a).x) / len : Vector2.zero;
                for (int s = 0; s <= steps; s++)
                {
                    var p = Vector2.Lerp(a, b, s / (float)steps);
                    for (float k = -width / 2f; k <= width / 2f; k += 0.5f / Px) Put(f, p.x + n.x * k, p.y + n.y * k, c);
                }
            }
            for (int f = 0; f < plan.Floors; f++)
                for (int x = 0; x < plan.NX; x++)
                    for (int z = 0; z < plan.NZ; z++)
                    {
                        var k = plan.KindAt(f, x, z);
                        Color c;
                        if (k == CellKind.Room)
                        {
                            var r = plan.Rooms[plan.IdAt(f, x, z)];
                            c = TypeColor(r.Type) * (r.Dark ? 0.6f : 1f);
                        }
                        else if (k == CellKind.Pocket) c = new Color(0.55f, 0.55f, 0.55f);
                        else if (k == CellKind.Void) c = ((x + z) % 4 < 2) ? new Color(0.15f, 0.2f, 0.4f) : new Color(0.25f, 0.3f, 0.55f);
                        else continue;
                        for (int i = 0; i < Px / 2; i++)
                            for (int j = 0; j < Px / 2; j++) Put(f, x * HousePlan.Res + (i + 0.5f) / Px, z * HousePlan.Res + (j + 0.5f) / Px, c);
                    }
            for (int i = 0; i < plan.Walls.Count; i++)
            {
                var w = plan.Walls[i];
                Line(w.Floor, w.A, w.B, w.Railing ? new Color(0.2f, 0.4f, 1f) : Color.black, w.Exterior ? 0.4f : 0.22f);
                foreach (var o in plan.OpeningsOf(i))
                {
                    var color = o.Kind switch
                    {
                        EdgeType.Door => new Color(0.1f, 0.75f, 0.2f),
                        EdgeType.Open => new Color(0.95f, 0.95f, 0.8f),
                        EdgeType.Vent => new Color(1f, 0.55f, 0.1f),
                        _ => new Color(0.6f, 0.2f, 0.9f),
                    };
                    float half = o.Kind == EdgeType.Open ? w.Length / 2f : LevelMetrics.ClearWidth(o.Width) / 2f;
                    Line(w.Floor, w.At(Mathf.Max(0f, o.At - half)), w.At(Mathf.Min(w.Length, o.At + half)), color, 0.5f);
                }
            }
            foreach (var s in l.Stairs)
            {
                var a = PlanGenerator.StairArea(s, 0f);
                foreach (int f in new[] { s.Floor, s.Floor + 1 })
                {
                    var c = f == s.Floor ? new Color(0.95f, 0.75f, 0.1f) : new Color(0.85f, 0.55f, 0.1f);
                    Line(f, new Vector2(a.xMin, a.yMin), new Vector2(a.xMax, a.yMin), c, 0.25f);
                    Line(f, new Vector2(a.xMax, a.yMin), new Vector2(a.xMax, a.yMax), c, 0.25f);
                    Line(f, new Vector2(a.xMax, a.yMax), new Vector2(a.xMin, a.yMax), c, 0.25f);
                    Line(f, new Vector2(a.xMin, a.yMax), new Vector2(a.xMin, a.yMin), c, 0.25f);
                    Line(f, new Vector2(a.xMin, a.yMin), new Vector2(a.xMax, a.yMax), c, 0.15f);
                }
            }
            foreach (var p in l.Props)
            {
                int f = LevelLayout.FloorOf(p.Position.y);
                var c = p.Kind == PropKind.Hatch ? Color.white : new Color(0.25f, 0.25f, 0.3f);
                for (float dx = -0.25f; dx <= 0.25f; dx += 0.12f)
                    for (float dz = -0.25f; dz <= 0.25f; dz += 0.12f) Put(f, p.Position.x + dx, p.Position.z + dz, c);
            }
            foreach (var r in l.RobotSpawns)
                for (float dx = -0.4f; dx <= 0.4f; dx += 0.12f)
                    for (float dz = -0.4f; dz <= 0.4f; dz += 0.12f) Put(r.Floor, r.Position.x + dx, r.Position.z + dz, new Color(0.9f, 0.1f, 0.1f));
            tex.SetPixels(px);
            tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }
    }
}
