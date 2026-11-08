using System;
using System.Collections;
using System.IO;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Net;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Command line helpers for quick testing of builds:
    ///   -host-local                 start a local (UDP) host
    ///   -join-local 127.0.0.1       join a local host
    ///   -autotest name -shotdir dir run a scripted walkthrough, log results with [AUTOTEST] and save screenshots
    /// </summary>
    public class DevAutomation : MonoBehaviour
    {
        string m_Name;
        string m_ShotDir;

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            if (i < 0) return null;
            return i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "";
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(0.5f);
            if (Arg("-host-local") != null) GameNetwork.Instance.StartHostLocal();
            var join = Arg("-join-local");
            if (join != null) GameNetwork.Instance.StartClientLocal(string.IsNullOrEmpty(join) ? "127.0.0.1" : join);

            m_Name = Arg("-autotest");
            if (m_Name == null) yield break;
            m_ShotDir = Arg("-shotdir") ?? Path.Combine(Application.persistentDataPath, "shots");
            Directory.CreateDirectory(m_ShotDir);
            yield return RunTest();
        }

        void Log(string msg) => Debug.Log($"[AUTOTEST {m_Name}] {msg}");

        IEnumerator Shot(string label)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(m_ShotDir, $"{m_Name}_{label}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("screenshot " + path);
            yield return null;
        }

        IEnumerator RunTest()
        {
            float timeout = Time.time + 30f;
            while ((PlayerController.Local == null || LevelGenerator.Current == null || GameManager.Instance == null) && Time.time < timeout)
                yield return null;
            var me = PlayerController.Local;
            if (me == null) { Log("FAIL: no local player"); Quit(); yield break; }
            yield return new WaitForSeconds(2f);
            var gm = GameManager.Instance;
            var layout = LevelGenerator.Current;
            Log($"connected: players={PlayerController.All.Count} level={gm.Level.Value.Index} seed={gm.Level.Value.Seed} rooms={layout.Width}x{layout.Height} " +
                $"items={WorldItem.All.Count} robots={RobotController.All.Count} cameras={FindObjectsByType<SecurityCamera>().Length} " +
                $"lasers={FindObjectsByType<LaserGate>().Length} quota={gm.Quota.Value}");
            yield return Shot("01_spawn");

            // Pick up the nearest item through the same RPC the UI uses.
            var inv = me.GetComponent<PlayerInventory>();
            // Each player takes a different item so that the host and the client do not race for the same one.
            var item = WorldItem.All.OrderBy(i => Vector3.Distance(i.transform.position, me.transform.position))
                .Skip((int)me.OwnerClientId).FirstOrDefault();
            if (item != null)
            {
                var itemPos = item.transform.position;
                var stand = itemPos + (me.transform.position - itemPos).WithY(0).normalized * 1.6f;
                stand.y = 0.05f;
                me.TeleportOwner(stand, Quaternion.LookRotation((itemPos - stand).WithY(0)));
                yield return new WaitForSeconds(1f);
                yield return Shot("02_item");
                if (inv.FindSpot(item.Shape.Value, out var x, out var y, out var rot))
                    inv.PickupRpc(item.NetworkObject, x, y, rot);
                yield return new WaitForSeconds(1f);
                Log($"pickup: inventory={inv.Items.Count} value={inv.TotalValue} itemStillInWorld={item != null && item.IsSpawned}");

                // Hand in at the hatch.
                me.TeleportOwner(layout.HatchPosition + new Vector3(1.2f, 0.05f, 0), Quaternion.LookRotation(Vector3.left));
                yield return new WaitForSeconds(1f);
                int before = gm.Collected.Value;
                if (inv.Items.Count > 0) inv.DepositRpc(0);
                yield return new WaitForSeconds(1f);
                Log($"deposit: collected {before} -> {gm.Collected.Value}, inventory={inv.Items.Count}");
                yield return Shot("03_hatch");
            }
            else Log("no items found");

            // Stand in front of a guard robot.
            var guard = RobotController.All.FirstOrDefault(r => r.HasEyes) ?? RobotController.All.FirstOrDefault();
            if (guard != null)
            {
                int hpBefore = me.Hp.Value;
                var gp = guard.transform.position;
                var spot = gp + guard.transform.forward * 5f;
                spot.y = 0.05f;
                me.TeleportOwner(spot, Quaternion.LookRotation((gp - spot).WithY(0)));
                yield return new WaitForSeconds(0.6f);
                yield return Shot("04_robot");
                float until = Time.time + 6f;
                while (Time.time < until && guard != null && guard.State.Value != RobotState.Chase) yield return null;
                Log($"robot '{guard.name}': state={guard.State.Value}");
                yield return new WaitForSeconds(3f);
                yield return Shot("05_robot_after");
                Log($"robot attack: hp {hpBefore} -> {me.Hp.Value}, downed={me.Downed.Value}");
            }

            // Camera.
            var cam = FindObjectsByType<SecurityCamera>().FirstOrDefault();
            if (cam != null && !me.Downed.Value)
            {
                var c = cam.transform.position;
                var spot = new Vector3(c.x, 0.05f, c.z) + cam.transform.forward * 5f;
                me.TeleportOwner(spot, Quaternion.LookRotation((c - spot).WithY(0)));
                float until = Time.time + 5f;
                while (Time.time < until && cam.Alert.Value != 2) yield return null;
                Log($"camera: alert={cam.Alert.Value} alarmActive={gm.AlarmActive}");
                yield return Shot("06_camera");
            }

            // Laser.
            var laser = FindObjectsByType<LaserGate>().FirstOrDefault();
            if (laser != null)
            {
                int hpBefore = me.Hp.Value;
                var lp = laser.transform.position;
                me.TeleportOwner(lp - laser.transform.forward * 2.5f + Vector3.up * 0.05f, Quaternion.LookRotation(laser.transform.forward));
                yield return new WaitForSeconds(0.8f);
                yield return Shot("07_laser");
                float until = Time.time + 5f;
                while (Time.time < until && !laser.BeamsOn) yield return null;
                me.TeleportOwner(lp + Vector3.up * 0.05f, Quaternion.LookRotation(laser.transform.forward));
                yield return new WaitForSeconds(0.6f);
                Log($"laser kind={laser.Kind.Value}: hp {hpBefore} -> {me.Hp.Value}, downed={me.Downed.Value}");
            }

            // Gas outside.
            var outside = new Vector3(-6f, 0.05f, layout.HatchPosition.z);
            me.TeleportOwner(outside, Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(10f);
            Log($"gas: time={me.GasTime:0.0} laughing={me.Laughing}");
            yield return Shot("08_gas");

            Log("DONE");
            yield return new WaitForSeconds(Arg("-autotest-linger") != null ? 20f : 2f);
            Quit();
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

    static class VectorExt
    {
        public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z);
    }
}
