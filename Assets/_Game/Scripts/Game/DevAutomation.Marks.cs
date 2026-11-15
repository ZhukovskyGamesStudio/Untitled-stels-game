using System.Collections;
using System.Linq;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Marking a thing: the player looks at a piece of loot and marks it (the real key), it goes on the x-ray layer and
        /// a ping appears; from the next room, behind a wall, it shows through in yellow-green.
        /// </summary>
        IEnumerator MarksScenario()
        {
            DevFlags.FreezeRobots = true;
            DevFlags.Peaceful = true;
            var l = ActiveLevel.Layout;
            foreach (var item in WorldItem.All.ToArray())
            {
                if (item == null) continue;
                var at = item.transform.position;
                var room = l.Plan.RoomAt(at + Vector3.up * 0.3f);
                if (room == null) continue;
                // a spot in another room on this floor with a wall in between
                Vector3 behind = default;
                bool found = false;
                for (int k = 0; k < 16 && !found; k++)
                    foreach (float d in new[] { 3f, 4.5f, 6f })
                    {
                        var dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                        var p = at + dir * d;
                        p.y = LevelLayout.FloorY(room.Floor);
                        var other = l.Plan.RoomAt(p + Vector3.up * 0.3f);
                        if (other == null || other.Id == room.Id || !l.Plan.IsFloor(room.Floor, new Vector2(p.x, p.z))) continue;
                        if (!Physics.Linecast(p + Vector3.up * 1.6f, at, ~0, QueryTriggerInteraction.Ignore)) continue;
                        behind = p;
                        found = true;
                        break;
                    }
                if (!found) continue;
                var near = at - (behind - at).normalized * 1.6f;
                near.y = LevelLayout.FloorY(room.Floor);
                LookFrom(near + Vector3.up * 0.05f, at);
                yield return new WaitForSeconds(0.4f);
                yield return PressKey(Controls.Get(GameAction.Mark).Key);
                yield return new WaitForSeconds(0.4f);
                var r = item.GetComponentInChildren<Renderer>();
                bool lit = r != null && r.gameObject.layer == MarkedThings.XrayLayer;
                int pings = GameManager.Instance.Pings.Active.Count(p => p.Kind == PingKind.Thing);
                Log($"marked {ItemCatalog.Describe(item.Shape, item.Color)}: x-ray layer {lit}, thing pings {pings} -> {(lit && pings > 0 ? "MARKED" : "FAIL")}");
                yield return Shot("marked_near");
                LookFrom(behind + Vector3.up * 0.05f, at);
                yield return new WaitForSeconds(0.4f);
                yield return Shot("marked_through_wall");
                yield break;
            }
            Log("FAIL: no item with a wall to look through");
        }
    }
}
