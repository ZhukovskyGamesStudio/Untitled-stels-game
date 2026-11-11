using System.Collections;
using System.Linq;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>A pressure plate in its doorway, seen as a player walking up to it (<c>LEVEL=4 Tools/dev.sh run plate</c>).</summary>
        IEnumerator PlateScenario()
        {
            if (!Me.IsServer) yield break;
            DevFlags.Peaceful = true;
            var plates = Detector.All.OfType<PressurePlate>().ToList();
            Log($"plates: {plates.Count}");
            int n = 0;
            foreach (var plate in plates.Take(3))
            {
                var p = plate.transform.position;
                var across = plate.transform.right; // the door's wall runs along local X: walk up to it across
                var dir = Vector3.Cross(Vector3.up, across).normalized;
                var from = p + dir * 3.2f + Vector3.up * 0.05f;
                LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(p - from))), p);
                yield return new WaitForSeconds(0.6f);
                yield return Shot($"plate_{n}_walking_up");
                // Up close, looking down at the floor in front of the feet: the seams and the dot are there to be found.
                var near = p + dir * 1.6f + Vector3.up * 0.05f;
                LookAtFrom(new Pose(near, Quaternion.LookRotation(Flat(p - near))), p);
                yield return new WaitForSeconds(0.8f);
                yield return Shot($"plate_{n++}_looking_down");
            }
        }

        /// <summary>
        /// Walking legs (<c>Tools/dev.sh run legs</c>): a guard and a hound walk past the camera (feet planted, knees bent),
        /// the spider crawls on its wall. A few frames of each, side on.
        /// </summary>
        IEnumerator LegsScenario()
        {
            if (!Me.IsServer) yield break;
            var gm = GameManager.Instance;
            DevFlags.Peaceful = true;
            foreach (var d in Detector.All.ToArray()) Session.Despawn(d.gameObject);
            foreach (var gate in FindObjectsByType<LaserGate>()) Session.Despawn(gate.gameObject);
            yield return new WaitForSeconds(0.5f);
            var layout = ActiveLevel.Layout;
            var room = layout.CellCenter(0, layout.StartCell.x, layout.StartCell.y);

            foreach (var kind in new[] { RobotKind.Guard, RobotKind.Hound })
            {
                var from = room + new Vector3(-3.5f, 0f, 1.5f);
                var obj = gm.SpawnRobotServer(kind, from, Quaternion.LookRotation(Vector3.right));
                var robot = obj.GetComponent<RobotController>();
                yield return new WaitForSeconds(0.8f);
                LookAtFrom(new Pose(room + new Vector3(0f, 0.05f, -2.8f), Quaternion.LookRotation(Vector3.forward)), room + new Vector3(0f, 0.6f, 1.5f));
                robot.InvestigateServer(room + new Vector3(4f, 0f, 1.5f), false);
                for (int i = 0; i < 4; i++)
                {
                    yield return new WaitForSeconds(0.45f);
                    yield return Shot($"{kind}_walk_{i}");
                }
                var legs = obj.GetComponent<ProceduralLegs>();
                Log($"{kind}: legs {(legs != null ? legs.Legs.Length : 0)}, speed {obj.GetComponent<RobotAnimator>().Speed:0.0}");
                Session.Despawn(obj);
                yield return new WaitForSeconds(0.3f);
            }

            var spider = gm.SpawnRobotServer(RobotKind.Spider, room + Vector3.up, Quaternion.identity);
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 3; i++)
            {
                var p = spider.transform.position;
                var toRoom = Flat(room - p).normalized;
                LookAtFrom(new Pose(new Vector3(p.x, room.y + 0.05f, p.z) + toRoom * 2.2f, Quaternion.LookRotation(-toRoom)), p);
                yield return new WaitForSeconds(0.1f);
                yield return Shot($"Spider_wall_{i}");
                yield return new WaitForSeconds(0.6f);
            }
            Log($"spider: legs {spider.GetComponent<ProceduralLegs>()?.Legs.Length}");
        }
    }
}
