using System.Collections;
using System.Linq;
using Stealth.Audio;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Knocked down by a hit (robot leaves, «устранён», getting up), the sweeper's floor lidar (floor vs a table top),
        /// the seismic wave (walking vs standing still), a hacked worker dropping its cargo.
        /// </summary>
        IEnumerator NewBotsScenario()
        {
            DevFlags.SkipGracePeriod = true;
            DevFlags.FreezeRobots = true;
            foreach (var r in RobotController.Robots.ToArray()) Session.Despawn(r.gameObject);
            var gm = GameManager.Instance;
            var at = ActiveLevel.Layout.HatchPosition;
            yield return new WaitForSeconds(0.5f);

            // 1. A guard hits the player: down with 1 HP, the robot loses interest, the player gets up.
            {
                Me.TeleportOwner(at + new Vector3(0, 0.05f, 0), Quaternion.LookRotation(Vector3.forward));
                var guard = gm.SpawnRobotServer(RobotKind.Guard, at + new Vector3(0, 0, 2.5f), Quaternion.LookRotation(Vector3.back)).GetComponent<RobotController>();
                int voices = AudioDirector.Instance != null ? AudioDirector.Instance.VoiceLines : 0;
                yield return new WaitForSeconds(4f); // the level-start grace is over
                DevFlags.FreezeRobots = false;
                DevFlags.HoldRobots = true; // let it notice from where it stands
                float t0 = Time.time;
                while (Time.time - t0 < 10f && guard.State != RobotState.Chase) yield return null;
                DevFlags.HoldRobots = false;
                while (Time.time - t0 < 15f && !Me.Downed) yield return null;
                float hitAfter = Time.time - t0;
                yield return Shot("knocked_down");
                yield return new WaitForSeconds(2.5f);
                Log($"knockdown: downed={Me.Downed} hp={Me.Hp} after {hitAfter:0.0} s; robot then {guard.State}, awareness {guard.Awareness:0.00}; " +
                    $"can get up={Me.CanGetUp}; voice lines {(AudioDirector.Instance != null ? AudioDirector.Instance.VoiceLines : 0) - voices}");
                yield return new WaitForSeconds(3f);
                Log($"knockdown: 5 s later the guard is {guard.State}, {Vector3.Distance(guard.transform.position, Me.transform.position):0.0} m away; player still down={Me.Downed}");
                if (Me.IsServer) Me.OnGetUpRequest();
                yield return new WaitForSeconds(0.3f);
                Log($"knockdown: after Space downed={Me.Downed} hp={Me.Hp}");
                DevFlags.FreezeRobots = true;
                Session.Despawn(guard.gameObject);
                if (Me.IsServer) Me.ResetForLevelServer();
                yield return new WaitForSeconds(0.5f);
            }

            // 2. The sweeper: feet on the floor are seen, a player on a table is not.
            {
                DevFlags.HoldRobots = true;
                var table = Instantiate(gm.Art.PingPongTable, at + new Vector3(-3.2f, 0f, 2.5f), Quaternion.identity);
                Physics.SyncTransforms();
                var sweeper = gm.SpawnRobotServer(RobotKind.Sweeper, at + new Vector3(0, 0, 2.5f), Quaternion.identity).GetComponent<RobotController>();
                LookAtFrom(new Pose(at + new Vector3(0, 0.05f, -1.5f), Quaternion.LookRotation(Vector3.forward)), sweeper.transform.position);
                yield return new WaitForSeconds(0.6f);
                DevFlags.FreezeRobots = false;
                float t0 = Time.time;
                while (Time.time - t0 < 8f && sweeper.State != RobotState.Alerted && sweeper.State != RobotState.Chase) yield return null;
                yield return Shot("sweeper_floor");
                Log($"sweeper: player on the floor 4 m away → {sweeper.State} after {Time.time - t0:0.0} s");
                DevFlags.FreezeRobots = true;
                Session.Despawn(sweeper.gameObject);
                yield return new WaitForSeconds(0.3f);

                sweeper = gm.SpawnRobotServer(RobotKind.Sweeper, at + new Vector3(0, 0, 2.5f), Quaternion.identity).GetComponent<RobotController>();
                Me.TeleportOwner(table.transform.position + new Vector3(0.6f, 0.8f, 0f), Quaternion.LookRotation(Vector3.right));
                yield return new WaitForSeconds(0.8f);
                DevFlags.FreezeRobots = false;
                t0 = Time.time;
                float max = 0f;
                while (Time.time - t0 < 6f) { max = Mathf.Max(max, sweeper.Suspicion); yield return null; }
                yield return Shot("sweeper_on_table");
                Log($"sweeper: player on the table (feet at {Me.transform.position.y - at.y:0.00} m) → {sweeper.State}, max suspicion {max:0.00}");
                DevFlags.FreezeRobots = true;
                DevFlags.HoldRobots = false;
                Session.Despawn(sweeper.gameObject);
                Destroy(table);
                if (Me.IsServer) Me.ResetForLevelServer();
                yield return new WaitForSeconds(0.5f);
            }

            // 3. Seismic: waves feel a walking player, not one standing still.
            {
                DevFlags.HoldRobots = true;
                var thumper = gm.SpawnRobotServer(RobotKind.Thumper, at + new Vector3(0, 0, 4.5f), Quaternion.LookRotation(Vector3.back)).GetComponent<RobotController>();
                Me.TeleportOwner(at + new Vector3(-1f, 0.05f, 0f), Quaternion.LookRotation(Vector3.right));
                yield return new WaitForSeconds(0.5f);
                thumper.Brain?.Reset(Time.time);
                DevFlags.FreezeRobots = false;
                float t0 = Time.time, still = 0f;
                while (Time.time - t0 < 6f) { still = Mathf.Max(still, thumper.Suspicion); yield return null; }
                yield return Shot("seismic_wave");
                thumper.Brain?.Reset(Time.time);
                t0 = Time.time;
                float walking = 0f;
                while (Time.time - t0 < 6f && thumper.State != RobotState.Alerted)
                {
                    Drive(new Vector2(0, Mathf.Sin((Time.time - t0) * 1.6f) > 0 ? 1 : -1));
                    walking = Mathf.Max(walking, thumper.Suspicion);
                    yield return null;
                }
                Release();
                Log($"seismic: standing still 6 s → max suspicion {still:0.00}; walking → {walking:0.00}, {thumper.State}");
                DevFlags.FreezeRobots = true;
                DevFlags.HoldRobots = false;
                Session.Despawn(thumper.gameObject);
                if (Me.IsServer) Me.ResetForLevelServer();
                yield return new WaitForSeconds(0.5f);
            }

            // 4. A worker with cargo drops it when hacked.
            {
                RobotController worker = null;
                for (int i = 0; i < 12 && worker == null; i++)
                {
                    var w = gm.SpawnRobotServer(RobotKind.Worker, at + new Vector3(2.5f, 0, 2.5f), Quaternion.identity).GetComponent<RobotController>();
                    yield return null;
                    if (w.Cargo >= 0) worker = w;
                    else Session.Despawn(w.gameObject);
                }
                if (worker == null) Log("cargo: no worker spawned with cargo in 12 tries");
                else
                {
                    LookAtFrom(ViewPoint(worker.transform.position, 2.5f), worker.transform.position + Vector3.up * 1.2f);
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot("worker_cargo");
                    int before = WorldItem.All.Count;
                    var (shape, color) = Detector.DecodeCargo(worker.Cargo);
                    worker.HackServer(5f);
                    yield return new WaitForSeconds(0.5f);
                    var dropped = WorldItem.All.FirstOrDefault(i => Vector3.Distance(i.transform.position, worker.transform.position) < 2f);
                    Log($"cargo: worker carried {ItemCatalog.Describe(shape, color)}; hacked → cargo {worker.Cargo}, items {before} → {WorldItem.All.Count}, " +
                        $"dropped next to it={dropped != null}");
                    yield return Shot("worker_dropped");
                }
            }
        }
    }
}
