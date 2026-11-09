using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Command line helpers for quick testing of builds:
    ///   -host-local / -join-local 127.0.0.1     start a local (UDP) host / client
    ///   -autotest &lt;scenario&gt; -shotdir dir      run a scripted scenario, log [AUTOTEST] lines, save screenshots
    ///   -autotest-linger                        keep running 20 s after the scenario (host waiting for clients)
    /// </summary>
    public partial class DevAutomation : MonoBehaviour
    {
        string m_Name;
        string m_ShotDir;
        int m_ShotIndex;
        readonly Dictionary<string, Func<IEnumerator>> m_Scenarios = new();

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            if (i < 0) return null;
            return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "";
        }

        void Awake()
        {
            m_Scenarios["full"] = FullScenario;
            m_Scenarios["client"] = FullScenario;
            m_Scenarios["coop"] = CoopScenario;
            m_Scenarios["move"] = MoveScenario;
            RegisterExtraScenarios();
        }

        partial void RegisterExtraScenarios();

        IEnumerator Start()
        {
            if (int.TryParse(Arg("-seed"), out var seed)) DevFlags.ForcedSeed = seed;
            DevFlags.Peaceful = Arg("-peaceful") != null;
            if (int.TryParse(Arg("-level"), out var level)) DevFlags.ForcedLevel = level;
            m_Name = Arg("-autotest");
            if (m_Name != null)
            {
                m_ShotDir = Arg("-shotdir") ?? Path.Combine(Application.persistentDataPath, "shots");
                Directory.CreateDirectory(m_ShotDir);
            }
            yield return new WaitForSeconds(0.5f);
            if (m_Name == "menu")
            {
                yield return new WaitForSeconds(0.5f);
                yield return Shot("main_menu");
            }
            if (Arg("-host-local") != null) Session.Launcher.HostLocal();
            var join = Arg("-join-local");
            if (join != null) Session.Launcher.JoinLocal(string.IsNullOrEmpty(join) ? "127.0.0.1" : join);
            if (m_Name == null) yield break;

            float timeout = Time.time + 30f;
            while ((PlayerController.Local == null || ActiveLevel.Layout == null || GameManager.Instance == null) && Time.time < timeout)
                yield return null;
            if (PlayerController.Local == null) { Log("FAIL: no local player"); Quit(); yield break; }
            yield return new WaitForSeconds(2f);

            if (m_Scenarios.TryGetValue(m_Name, out var scenario)) yield return scenario();
            else Log($"FAIL: unknown scenario '{m_Name}'");

            DevFlags.Autopilot = null;
            Log("DONE");
            yield return new WaitForSeconds(Arg("-autotest-linger") != null ? 20f : 1f);
            Quit();
        }

        void Log(string msg) => Debug.Log($"[AUTOTEST {m_Name}] {msg}");

        IEnumerator Shot(string label)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(m_ShotDir, $"{m_Name}_{++m_ShotIndex:00}_{label}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
        }

        static PlayerController Me => PlayerController.Local;

        static void Drive(Vector2 move, bool sprint = false, bool crouch = false, Vector2 look = default) =>
            DevFlags.Autopilot = new PlayerInputState { Move = move, Sprint = sprint, CrouchHeld = crouch, Look = look };

        static void Release() => DevFlags.Autopilot = new PlayerInputState();

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        /// <summary>A standing spot near <paramref name="target"/> on the open side (towards the room centre), facing it.</summary>
        static Pose ViewPoint(Vector3 target, float distance)
        {
            var center = ActiveLevel.Layout.RoomCenterAt(target);
            var dir = Flat(center - target);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var floorY = Mathf.Floor((target.y + 0.5f) / LevelMetrics.FloorHeight) * LevelMetrics.FloorHeight;
            var stand = target + dir.normalized * Mathf.Min(distance, dir.magnitude + 1f);
            stand.y = floorY + 0.05f;
            return new Pose(stand, Quaternion.LookRotation(Flat(target - stand)));
        }

        /// <summary>Teleports in front of a world point and pitches the camera to look right at it.</summary>
        void LookAtFrom(Pose pose, Vector3 target)
        {
            Me.TeleportOwner(pose.position, pose.rotation);
            var head = pose.position + Vector3.up * 1.6f;
            var to = target - head;
            Me.Motor.SetPitch(-Mathf.Atan2(to.y, Flat(to).magnitude) * Mathf.Rad2Deg);
        }

        // ------------------------------------------------------------------ scenarios

        IEnumerator MoveScenario()
        {
            var layout = ActiveLevel.Layout;
            var start = layout.HatchPosition + new Vector3(-3.5f, 0.05f, 1.6f);
            Me.TeleportOwner(start, Quaternion.LookRotation(Vector3.right));
            Release();
            yield return new WaitForSeconds(0.5f);

            // Acceleration from rest.
            Drive(Vector2.up);
            float t0 = Time.time, t90 = -1f;
            while (Time.time - t0 < 1f)
            {
                if (t90 < 0 && Me.Motor.HorizontalSpeed >= Me.Motor.WalkSpeed * 0.9f) t90 = Time.time - t0;
                yield return null;
            }
            Log($"walk: 90% speed after {t90:0.000}s, speed {Me.Motor.HorizontalSpeed:0.00}");

            // Stop distance.
            var p0 = Me.transform.position;
            Release();
            yield return new WaitForSeconds(0.5f);
            Log($"stop: slid {Vector3.Distance(Flat(p0), Flat(Me.transform.position)):0.000} m");

            // Sprint & crouch.
            Me.TeleportOwner(start, Quaternion.LookRotation(Vector3.right));
            Drive(Vector2.up, sprint: true);
            yield return new WaitForSeconds(0.6f);
            Log($"sprint: speed {Me.Motor.HorizontalSpeed:0.00}");
            yield return Shot("sprint");
            Drive(Vector2.up, crouch: true);
            yield return new WaitForSeconds(0.6f);
            Log($"crouch: speed {Me.Motor.HorizontalSpeed:0.00}, height {Me.GetComponent<CharacterController>().height:0.00}");
            yield return Shot("crouch");
            Release();

            // Mouse look.
            float yaw0 = Me.transform.eulerAngles.y;
            Drive(Vector2.zero, look: new Vector2(10f, 0f));
            yield return null;
            Release();
            Log($"look: yaw changed by {Mathf.DeltaAngle(yaw0, Me.transform.eulerAngles.y):0.0} deg for 10 px");
            yield return MoveScenarioExtra();
        }

        partial void MoveScenarioExtraImpl(List<IEnumerator> steps);

        IEnumerator MoveScenarioExtra()
        {
            var steps = new List<IEnumerator>();
            MoveScenarioExtraImpl(steps);
            foreach (var s in steps) yield return s;
        }

        IEnumerator FullScenario()
        {
            var gm = GameManager.Instance;
            var layout = ActiveLevel.Layout;
            Log($"connected: players={PlayerController.All.Count} level={gm.Level.Index} seed={gm.Level.Seed} rooms={layout.Width}x{layout.Height}x{layout.Floors} " +
                $"items={WorldItem.All.Count} robots={RobotController.Robots.Count} cameras={FindObjectsByType<SecurityCamera>().Length} " +
                $"lasers={FindObjectsByType<LaserGate>().Length} quota={gm.Quota}");
            var kinds = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var d in Detector.All)
            {
                string k = d.Kind.ToString();
                kinds[k] = kinds.TryGetValue(k, out var n) ? n + 1 : 1;
            }
            Log("security on this level: " + string.Join(", ", kinds.Select(kv => $"{kv.Key}x{kv.Value}")));
            yield return Shot("spawn");

            // Pick up an item through the same RPC the UI uses; each player takes a different one.
            var inv = Me.GetComponent<PlayerInventory>();
            var item = WorldItem.All.OrderBy(i => Vector3.Distance(i.transform.position, Me.transform.position))
                .Skip((int)Me.ClientId).FirstOrDefault();
            if (item != null)
            {
                var itemPos = item.transform.position;
                var stand = itemPos + Flat(Me.transform.position - itemPos).normalized * 1.6f;
                stand.y = itemPos.y < 0.5f ? itemPos.y + 0.05f : Mathf.Floor(itemPos.y / LevelMetrics.FloorHeight) * LevelMetrics.FloorHeight + 0.05f;
                Me.TeleportOwner(stand, Quaternion.LookRotation(Flat(itemPos - stand)));
                yield return new WaitForSeconds(1f);
                yield return Shot("item");
                if (inv.FindSpot(item.Shape, out var x, out var y, out var rot)) inv.RequestPickup(item, x, y, rot);
                yield return new WaitForSeconds(1f);
                Log($"pickup: inventory={inv.Items.Count} value={inv.TotalValue} itemStillInWorld={item != null && item.isActiveAndEnabled}");

                Me.TeleportOwner(layout.HatchPosition + new Vector3(1.2f, 0.05f, 0), Quaternion.LookRotation(Vector3.left));
                yield return new WaitForSeconds(1f);
                int before = gm.Collected;
                if (inv.Items.Count > 0) inv.RequestDeposit(0);
                yield return new WaitForSeconds(1f);
                Log($"deposit: collected {before} -> {gm.Collected}, inventory={inv.Items.Count}");
                yield return Shot("hatch");
            }
            else Log("no items found");

            var guard = RobotController.Robots.FirstOrDefault(r => r.Senses.HasEyes) ?? RobotController.Robots.FirstOrDefault();
            if (guard != null)
            {
                int hpBefore = Me.Hp;
                var gp = guard.transform.position;
                var spot = gp + guard.transform.forward * 5f;
                spot.y = gp.y + 0.05f;
                Me.TeleportOwner(spot, Quaternion.LookRotation(Flat(gp - spot)));
                yield return new WaitForSeconds(0.6f);
                yield return Shot("robot");
                float until = Time.time + 6f;
                while (Time.time < until && guard != null && guard.State != RobotState.Chase) yield return null;
                Log($"robot '{guard.name}': state={guard.State}");
                yield return new WaitForSeconds(3f);
                Log($"robot attack: hp {hpBefore} -> {Me.Hp}, downed={Me.Downed}");
            }

            var cam = FindObjectsByType<SecurityCamera>().FirstOrDefault();
            if (cam != null && !Me.Downed)
            {
                var c = cam.transform.position;
                var spot = new Vector3(c.x, Mathf.Floor(c.y / LevelMetrics.FloorHeight) * LevelMetrics.FloorHeight + 0.05f, c.z) + cam.transform.forward * 5f;
                Me.TeleportOwner(spot, Quaternion.LookRotation(Flat(c - spot)));
                float until = Time.time + 5f;
                while (Time.time < until && cam.Alert != 2) yield return null;
                Log($"camera: alert={cam.Alert} alarmActive={gm.AlarmActive}");
                yield return Shot("camera");
            }

            var laser = FindObjectsByType<LaserGate>().FirstOrDefault();
            if (laser != null)
            {
                int hpBefore = Me.Hp;
                var lp = laser.transform.position;
                Me.TeleportOwner(lp - laser.transform.forward * 2.5f + Vector3.up * 0.05f, Quaternion.LookRotation(laser.transform.forward));
                yield return new WaitForSeconds(0.8f);
                yield return Shot("laser");
                float until = Time.time + 5f;
                while (Time.time < until && !laser.BeamsOn) yield return null;
                Me.TeleportOwner(lp + Vector3.up * 0.05f, Quaternion.LookRotation(laser.transform.forward));
                yield return new WaitForSeconds(0.6f);
                Log($"laser pattern={laser.Pattern}: hp {hpBefore} -> {Me.Hp}, downed={Me.Downed}");
            }

            var outside = new Vector3(-6f, 0.05f, layout.HatchPosition.z);
            Me.TeleportOwner(outside, Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(10f);
            Log($"gas: time={Me.Gas.Time:0.0} laughing={Me.Gas.Laughing}");
            yield return Shot("gas");
        }

        /// <summary>Two players: the host walks and looks around, the client watches and takes pictures.</summary>
        IEnumerator CoopScenario()
        {
            yield return CoopTeamShots();
            var hatch = ActiveLevel.Layout.HatchPosition;
            float t0 = Time.time;
            while (PlayerController.All.Count < 2 && Time.time - t0 < 20f) yield return null;
            if (Me.IsServer)
            {
                Me.TeleportOwner(hatch + new Vector3(-1f, 0.05f, -2.5f), Quaternion.LookRotation(Vector3.right));
                yield return new WaitForSeconds(4f);
                Drive(new Vector2(0, 0), look: new Vector2(0, -350f)); // look up 35 deg
                yield return null;
                Drive(new Vector2(-1, 0)); // strafe left, towards +z
                yield return new WaitForSeconds(1.2f);
                Drive(Vector2.zero, look: new Vector2(0, 700f)); // look down
                yield return null;
                Release();
                yield return new WaitForSeconds(4f);
            }
            else
            {
                Me.TeleportOwner(hatch + new Vector3(3.8f, 0.05f, 0f), Quaternion.LookRotation(Vector3.left));
                Me.Motor.SetPitch(3f);
                yield return new WaitForSeconds(4.6f);
                yield return Shot("host_looking_up_walking");
                yield return new WaitForSeconds(1.6f);
                yield return Shot("host_looking_down");
                var host = PlayerController.All.FirstOrDefault(p => p != Me);
                Log($"remote host pitch={host?.LookPitch}");
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
