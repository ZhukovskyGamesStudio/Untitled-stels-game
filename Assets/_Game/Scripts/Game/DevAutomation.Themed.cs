using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        static readonly RoomType[] ThemedTypes =
        {
            RoomType.Kitchen, RoomType.Restroom, RoomType.Gym, RoomType.Library, RoomType.CopyRoom, RoomType.ControlRoom, RoomType.Lab,
            RoomType.NapRoom, RoomType.Studio, RoomType.LockerRoom, RoomType.Infirmary, RoomType.GameRoom,
        };

        /// <summary>
        /// The themed rooms of this house in the current style: for every kind the lit room with the most furniture, shot
        /// from two of its corners at eye height, robots frozen (screenshots for the art review, not a check).
        /// </summary>
        IEnumerator ThemedScenario()
        {
            var l = ActiveLevel.Layout;
            DevFlags.FreezeRobots = true;
            int shots = 0;
            foreach (var type in ThemedTypes)
            {
                PlanRoom best = null;
                int bestCount = 0;
                foreach (var r in l.Rooms)
                {
                    if (r.Type != type || r.Dark || r.Area < 12f || r.Id == l.HiddenRoom) continue;
                    int count = l.Props.Count(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == r.Id);
                    if (count > bestCount) { best = r; bestCount = count; }
                }
                if (best == null) { Log($"themed: no lit {type} in this house"); continue; }
                int n = 0;
                foreach (var from in CornersOf(l, best))
                {
                    var to = best.Center + Vector3.up * 0.7f;
                    LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(to - from))), to);
                    yield return new WaitForSeconds(0.6f);
                    yield return Shot($"themed_{type}_{++n}");
                    shots++;
                    if (n >= 2) break;
                }
                var kinds = l.Props.Where(p => l.Plan.RoomAt(p.Position + Vector3.up * 0.3f)?.Id == best.Id).Select(p => p.Kind.ToString()).Distinct();
                Log($"themed: {type} #{best.Id} {best.Area:0} m², {bestCount} pieces: {string.Join(", ", kinds)}");
            }
            Log($"themed: {shots} shots");
        }

        /// <summary>Points 0.7 of the way from the middle to each corner of a room, on its floor and clear of furniture.</summary>
        static IEnumerable<Vector3> CornersOf(LevelLayout l, PlanRoom r)
        {
            var c = r.Center;
            var b = r.Bounds;
            foreach (var k in new[] { new Vector2(b.xMin, b.yMin), new Vector2(b.xMax, b.yMax), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMin) })
                foreach (float t in new[] { 0.75f, 0.6f, 0.45f })
                {
                    var from = Vector3.Lerp(c, new Vector3(k.x, c.y, k.y), t) + Vector3.up * 0.05f;
                    if (l.RoomAt(from) != r || l.NearStair(r.Floor, from, 0.5f)) continue;
                    if (l.Props.Any(p => (Flat(p.Position - from)).magnitude < 0.7f && LevelLayout.FloorOf(p.Position.y) == r.Floor)) continue;
                    yield return from;
                    break;
                }
        }
    }
}
