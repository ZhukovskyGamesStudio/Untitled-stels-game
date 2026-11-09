using System.Collections;
using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        partial void GalleryExtraImpl(RobotKind kind, GameObject obj, List<IEnumerator> steps)
        {
            if (Arg("-hostile") == null) return;
            if (kind == RobotKind.Thumper) steps.Add(SneakCheck(kind, obj, crouch: true));
            if (kind == RobotKind.Hound) { steps.Add(TrailCheck(obj)); return; }
            if (kind == RobotKind.Sorter) steps.Add(SorterCheck(obj));
            if (kind == RobotKind.Vacuum || kind == RobotKind.Lidar) { steps.Add(CrossLineCheck(kind, obj)); return; }
            if (kind == RobotKind.PressurePlate) { steps.Add(PlateCheck(obj)); return; }
            if (kind == RobotKind.MotionSensor) steps.Add(SneakCheck(kind, obj, crouch: true));
            steps.Add(DetectionCheck(kind, obj));
        }

        /// <summary>
        /// The robot is switched on, the player stands in front of it (walking in place to make some noise and
        /// vibration) and we log how it reacts: awareness, alarm, damage.
        /// </summary>
        IEnumerator DetectionCheck(RobotKind kind, GameObject obj)
        {
            var gm = GameManager.Instance;
            var aw = obj.GetComponent<Detector>();
            int hp0 = Me.Hp;
            gm.Run.CalmDown();
            var p = obj.transform.position;
            // Wall / ceiling crawlers look into the room (their "up"), everybody else looks forward.
            var front = kind == RobotKind.Spider ? obj.transform.up : obj.transform.forward;
            var stand = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)) + 0.05f, p.z) + Flat(front).normalized * 3.5f;
            Me.TeleportOwner(stand, Quaternion.LookRotation(Flat(p - stand)));
            yield return CustomDetectionSetup(kind, obj);
            DevFlags.FreezeRobots = false;
            float t0 = Time.time, aware = 0f, firstAware = -1f;
            while (Time.time - t0 < 6f)
            {
                // Shuffle back and forth: footsteps for microphones / seismic sensors, movement for PIR.
                Drive(new Vector2(Mathf.Sin(Time.time * 3f) > 0 ? 1 : -1, 0));
                float a = aw != null ? aw.AwarenessOf(Me.ClientId) : 0f;
                aware = Mathf.Max(aware, a);
                if (firstAware < 0 && a > 0.05f) firstAware = Time.time - t0;
                if (a >= 0.99f && (gm.AlarmActive || Me.Hp < hp0)) break;
                yield return null;
            }
            Release();
            yield return Shot($"kind_{kind}_reacting");
            Log($"detect {kind}: noticed after {firstAware:0.0}s, max awareness {aware:0.00}, alarm={gm.AlarmActive}, hp {hp0}->{Me.Hp}");
            DevFlags.FreezeRobots = true;
            if (Me.Hp < hp0 && Me.IsServer) Me.ResetForLevelServer();
            yield return new WaitForSeconds(0.3f);
        }

        partial void CustomDetectionSetupImpl(RobotKind kind, GameObject obj, List<IEnumerator> steps);

        IEnumerator CustomDetectionSetup(RobotKind kind, GameObject obj)
        {
            var steps = new List<IEnumerator>();
            CustomDetectionSetupImpl(kind, obj, steps);
            foreach (var s in steps) yield return s;
        }

        /// <summary>Moves near the robot in a specific way (e.g. crouching) and logs whether it notices.</summary>
        IEnumerator SneakCheck(RobotKind kind, GameObject obj, bool crouch)
        {
            var aw = obj.GetComponent<Detector>();
            var p = obj.transform.position;
            var stand = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)) + 0.05f, p.z) + Flat(obj.transform.right).normalized * 3.5f;
            Me.TeleportOwner(stand, Quaternion.LookRotation(Flat(obj.transform.forward)));
            DevFlags.FreezeRobots = false;
            float t0 = Time.time, max = 0f, maxSpeed = 0f, maxMotor = 0f;
            bool alwaysCrouched = true;
            while (Time.time - t0 < 4f)
            {
                Drive(new Vector2(0, Mathf.Sin(Time.time * 2f) > 0 ? 1 : -1), crouch: crouch);
                max = Mathf.Max(max, aw.AwarenessOf(Me.ClientId));
                if (Time.time - t0 > 0.2f)
                {
                    maxSpeed = Mathf.Max(maxSpeed, Me.GroundSpeed);
                    maxMotor = Mathf.Max(maxMotor, Me.Motor.HorizontalSpeed);
                    alwaysCrouched &= Me.Crouching == crouch;
                }
                yield return null;
            }
            Release();
            DevFlags.FreezeRobots = true;
            Log($"sneak past {kind} (crouch={crouch}): max awareness {max:0.00}, max speed server {maxSpeed:0.00} / motor {maxMotor:0.00}, posture held={alwaysCrouched}");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>Stand next to a scan line and wait for it to sweep over you (no jumping).</summary>
        IEnumerator CrossLineCheck(RobotKind kind, GameObject obj)
        {
            var gm = GameManager.Instance;
            gm.Run.CalmDown();
            var p = obj.transform.position;
            // Ceiling robots hang just under the next slab: measure the floor from a point below them.
            float below = kind == RobotKind.Vacuum ? 2.5f : 0f;
            var stand = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y - below)) + 0.05f, p.z) + new Vector3(1.5f, 0, 0.5f);
            LookAtFrom(new Pose(stand, Quaternion.LookRotation(Vector3.left)), new Vector3(p.x, stand.y, p.z));
            DevFlags.FreezeRobots = false;
            float t0 = Time.time;
            bool shot = false;
            while (Time.time - t0 < 8f && !gm.AlarmActive)
            {
                if (!shot && Time.time - t0 > 1f) { shot = true; yield return Shot($"kind_{kind}_scanning"); }
                yield return null;
            }
            Log($"{kind}: line swept over the player after {Time.time - t0:0.0}s, alarm={gm.AlarmActive}");
            DevFlags.FreezeRobots = true;
        }

        /// <summary>Jump over the plate (nothing happens), then walk over it (alarm).</summary>
        IEnumerator PlateCheck(GameObject obj)
        {
            var gm = GameManager.Instance;
            gm.Run.CalmDown();
            var p = obj.transform.position;
            DevFlags.FreezeRobots = false;
            LookAtFrom(new Pose(p + new Vector3(0, 0.05f, -1.6f), Quaternion.LookRotation(Vector3.forward)), p);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("kind_PressurePlate_close");
            // Run-up and jump over.
            Drive(Vector2.up, sprint: true);
            yield return new WaitForSeconds(0.08f);
            DevFlags.Autopilot = new Player.PlayerInputState { Move = Vector2.up, Sprint = true, Jump = true };
            yield return null;
            Drive(Vector2.up, sprint: true);
            yield return new WaitForSeconds(0.7f);
            Release();
            bool jumped = !gm.AlarmActive;
            yield return new WaitForSeconds(0.3f);
            Me.TeleportOwner(p + new Vector3(0, 0.05f, -1.6f), Quaternion.LookRotation(Vector3.forward));
            Drive(Vector2.up);
            yield return new WaitForSeconds(0.9f);
            Release();
            Log($"pressure plate: jumping over → no alarm={jumped}, walking over → alarm={gm.AlarmActive}");
            DevFlags.FreezeRobots = true;
        }

        /// <summary>Empty-handed in front of a sorter: ignored. Then the player picks up loot of its colour.</summary>
        IEnumerator SorterCheck(GameObject obj)
        {
            var robot = obj.GetComponent<RobotController>();
            var aw = obj.GetComponent<Detector>();
            var inv = Me.GetComponent<Player.PlayerInventory>();
            if (Me.IsServer) inv.ClearServer();
            var p = obj.transform.position;
            var stand = new Vector3(p.x, 0.05f, p.z) + Flat(obj.transform.forward).normalized * 4f;
            Me.TeleportOwner(stand, Quaternion.LookRotation(Flat(p - stand)));
            DevFlags.FreezeRobots = false;
            float t0 = Time.time, max = 0f;
            while (Time.time - t0 < 3f) { max = Mathf.Max(max, aw.AwarenessOf(Me.ClientId)); yield return null; }
            Log($"sorter (colour {robot.ColorFilter}): empty-handed max awareness {max:0.00}");
            DevFlags.FreezeRobots = true;
            // Give the player an item of the sorter's colour; the detection check that follows must now succeed.
            if (Me.IsServer) inv.GiveServer(0, (byte)robot.ColorFilter);
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>The player walks past the hound's nose and away out of the room; the hound must follow the trail.</summary>
        IEnumerator TrailCheck(GameObject obj)
        {
            var robot = obj.GetComponent<RobotController>();
            var p = obj.transform.position;
            var start = new Vector3(p.x, 0.05f, p.z) + Flat(obj.transform.right).normalized * 1.5f;
            Me.TeleportOwner(start, Quaternion.LookRotation(Flat(-obj.transform.forward)));
            // Walk away (towards the lobby side) for ~2.5 s, leaving a trail, with the robot still frozen.
            Drive(Vector2.up);
            yield return new WaitForSeconds(2.5f);
            Release();
            float before = Vector3.Distance(Flat(p), Flat(Me.transform.position));
            DevFlags.FreezeRobots = false;
            float t0 = Time.time;
            while (Time.time - t0 < 8f && robot.State != RobotState.Chase) yield return null;
            float tChase = Time.time - t0;
            yield return new WaitForSeconds(2f);
            float after = Vector3.Distance(Flat(obj.transform.position), Flat(Me.transform.position));
            yield return Shot("kind_Hound_tracking");
            Log($"hound: started tracking after {tChase:0.0}s, state={robot.State}, distance {before:0.0} -> {after:0.0} m");
            DevFlags.FreezeRobots = true;
            if (Me.IsServer) Me.ResetForLevelServer();
        }

        /// <summary>Three alarms → the hunter arrives.</summary>
        IEnumerator HunterScenario()
        {
            var gm = GameManager.Instance;
            DevFlags.SkipGracePeriod = true;
            var far = ActiveLevel.Layout.HatchPosition + Vector3.forward * 100f;
            for (int i = 0; i < 3; i++)
            {
                gm.RaiseAlarmServer(far, 1f);
                yield return new WaitForSeconds(4.3f);
            }
            yield return new WaitForSeconds(0.5f);
            RobotController hunter = null;
            foreach (var r in RobotController.Robots) if (r.name.Contains("Hunter")) hunter = r;
            Log($"hunter: present={hunter != null}" + (hunter != null ? $" at {hunter.transform.position}, state {hunter.State}" : ""));
            if (hunter != null)
            {
                DevFlags.FreezeRobots = true;
                var hp = hunter.transform.position;
                LookAtFrom(ViewPoint(hp, 3.5f), hp + Vector3.up * 1.4f);
                yield return new WaitForSeconds(0.8f);
                yield return Shot("hunter");
            }
        }

        /// <summary>The three laser gate kinds side by side (static low beam, blinking, moving).</summary>
        IEnumerator LasersScenario()
        {
            var gm = GameManager.Instance;
            var hatch = ActiveLevel.Layout.HatchPosition;
            for (int k = 0; k < 3; k++)
            {
                var pattern = (LaserPattern)k;
                Session.Spawn(gm.LaserPrefab, hatch + new Vector3(-2.5f + k * 2.5f, 0, 3f), Quaternion.identity,
                    go => go.GetComponent<LaserGate>().Configure(pattern, 1.8f));
            }
            LookAtFrom(new Pose(hatch + new Vector3(0, 0.05f, -1.5f), Quaternion.LookRotation(Vector3.forward)), hatch + new Vector3(0, 0.8f, 3f));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("lasers_a");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("lasers_b");
        }

        /// <summary>A guard spots the player and radios a listener standing nearby, which comes to check.</summary>
        IEnumerator RadioScenario()
        {
            var gm = GameManager.Instance;
            DevFlags.SkipGracePeriod = true;
            DevFlags.FreezeRobots = true;
            foreach (var r in RobotController.Robots.ToArray()) Session.Despawn(r.gameObject);
            var hatch = ActiveLevel.Layout.HatchPosition;
            var guard = gm.SpawnRobotServer(RobotKind.Guard, hatch + new Vector3(0, 0, 3f), Quaternion.LookRotation(Vector3.back)).GetComponent<RobotController>();
            var listener = gm.SpawnRobotServer(RobotKind.Listener, hatch + new Vector3(3.5f, 0, 3.5f), Quaternion.LookRotation(Vector3.forward)).GetComponent<RobotController>();
            LookAtFrom(new Pose(hatch + new Vector3(0, 0.05f, -1.5f), Quaternion.LookRotation(Vector3.forward)), hatch + new Vector3(0, 1.2f, 3f));
            yield return new WaitForSeconds(0.5f);
            DevFlags.FreezeRobots = false;
            float t0 = Time.time;
            while (Time.time - t0 < 5f && guard.State != RobotState.Chase) yield return null;
            yield return null;
            yield return Shot("radio_icons");
            Log($"radio: guard={guard.State}, listener after the call={listener.State}");
            DevFlags.FreezeRobots = true;
        }

        /// <summary>Hack a guard from behind and a turret up close, then check both are switched off.</summary>
        IEnumerator HackScenario()
        {
            var gm = GameManager.Instance;
            DevFlags.SkipGracePeriod = true;
            DevFlags.FreezeRobots = true;
            foreach (var r in RobotController.Robots.ToArray()) Session.Despawn(r.gameObject);
            var hatch = ActiveLevel.Layout.HatchPosition;
            var guard = gm.SpawnRobotServer(RobotKind.Guard, hatch + new Vector3(-2f, 0, 3f), Quaternion.LookRotation(Vector3.forward)).GetComponent<RobotController>();
            var turret = gm.SpawnRobotServer(RobotKind.Turret, hatch + new Vector3(2.5f, 0, 3f), Quaternion.LookRotation(Vector3.forward)).GetComponent<SecurityDevice>();
            // Behind the guard.
            LookAtFrom(new Pose(hatch + new Vector3(-2f, 0.05f, 1.8f), Quaternion.LookRotation(Vector3.forward)), hatch + new Vector3(-2f, 1.2f, 3f));
            yield return new WaitForSeconds(0.4f);
            string prompt = UI.GameHUD.Instance.CurrentPrompt.Text;
            DevFlags.AutoInteract = true;
            yield return new WaitForSeconds(1.2f);
            yield return Shot("hacking_progress");
            yield return new WaitForSeconds(1.6f);
            DevFlags.AutoInteract = false;
            yield return new WaitForSeconds(0.3f);
            yield return Shot("guard_hacked");
            Log($"hack guard: prompt='{prompt}' hacked={guard.IsHacked}");
            // Next to the turret.
            Me.TeleportOwner(hatch + new Vector3(2.5f, 0.05f, 1.6f), Quaternion.LookRotation(Vector3.forward));
            yield return new WaitForSeconds(0.3f);
            DevFlags.AutoInteract = true;
            yield return new WaitForSeconds(2.8f);
            DevFlags.AutoInteract = false;
            yield return new WaitForSeconds(0.3f);
            Log($"hack turret: hacked={turret.IsHacked}");
            // Robots wake up: a hacked turret must not see the player.
            DevFlags.FreezeRobots = false;
            yield return new WaitForSeconds(2f);
            Log($"hacked turret awareness while hacked: {turret.GetComponent<Detector>().AwarenessOf(Me.ClientId):0.00}");
        }
    }
}
