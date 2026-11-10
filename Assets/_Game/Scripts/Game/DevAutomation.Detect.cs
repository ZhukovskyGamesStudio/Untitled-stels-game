using System.Collections;
using System.Linq;
using Stealth.Audio;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// How long robots take to notice: a guard at different distances and stances (time to «Обнаружен нарушитель»,
        /// the warning before the chase, the suspicion bar), and a listener hearing walking vs sneaking.
        /// </summary>
        IEnumerator DetectScenario()
        {
            DevFlags.SkipGracePeriod = true;
            DevFlags.FreezeRobots = true;
            DevFlags.HoldRobots = true; // measure from where they stand
            foreach (var r in RobotController.Robots.ToArray()) Session.Despawn(r.gameObject);
            var at = ActiveLevel.Layout.HatchPosition;
            yield return SightCase(at, 5f, false, "guard_5m");
            yield return SightCase(at, 8f, false, "guard_8m");
            yield return SightCase(at, 5f, true, "guard_5m_crouched");
            yield return SightCase(at, 2f, false, "guard_2m_behind", facingAway: true);
            yield return SightCase(at, 5f, false, "guard_5m_lights_off", lightsOff: true);
            DevFlags.HoldRobots = false;
            yield return HearingCase(at, false, "listener_walk");
            yield return HearingCase(at, true, "listener_sneak");
        }

        /// <summary>The player stands still in plain view of a guard; how long until it warns, and how long the warning lasts.</summary>
        IEnumerator SightCase(Vector3 at, float distance, bool crouch, string label, bool facingAway = false, bool lightsOff = false)
        {
            var lamps = lightsOff ? FindObjectsByType<RoomLight>() : new RoomLight[0];
            foreach (var l in lamps) { l.enabled = false; l.Lamp.enabled = false; }
            var gm = GameManager.Instance;
            DevFlags.FreezeRobots = true;
            var robotPos = at + Vector3.forward * distance;
            var robot = gm.SpawnRobotServer(RobotKind.Guard, robotPos, Quaternion.LookRotation(facingAway ? Vector3.forward : Vector3.back))
                .GetComponent<RobotController>();
            LookAtFrom(new Pose(at + Vector3.up * 0.05f, Quaternion.LookRotation(Vector3.forward)), robotPos + Vector3.up * 1.4f);
            Drive(Vector2.zero, crouch: crouch);
            yield return new WaitForSeconds(0.8f);
            bool sees = robot.Look(Me, Tuning.Perception.Vision, out var g);
            int voices = AudioDirector.Instance != null ? AudioDirector.Instance.VoiceLines : 0;

            DevFlags.FreezeRobots = false;
            float t0 = Time.time;
            bool barShot = false;
            float nextTrace = 0f;
            while (Time.time - t0 < 20f && robot.State != RobotState.Alerted && robot.State != RobotState.Chase)
            {
                if (Time.time - t0 >= nextTrace)
                {
                    nextTrace += 2f;
                    bool now = robot.Look(Me, Tuning.Perception.Vision, out var gl);
                    Log($"  {label} t={Time.time - t0:0.0}: {robot.State} suspicion {robot.Suspicion:0.00}, sees={now} exposure={gl.Exposure:0.00} " +
                        $"off-centre={gl.OffCentre:0.00} crouching={Me.Crouching} distance={Vector3.Distance(robot.transform.position, Me.transform.position):0.0}");
                }
                if (!barShot && robot.Suspicion > 0.45f)
                {
                    barShot = true;
                    yield return Shot(label + "_bar");
                }
                yield return null;
            }
            float toAlert = Time.time - t0;
            bool alerted = robot.State == RobotState.Alerted;
            if (alerted) yield return Shot(label + "_alerted");
            float t1 = Time.time;
            while (Time.time - t1 < 5f && robot.State == RobotState.Alerted) yield return null;
            float warning = Time.time - t1;
            var state = robot.State;
            yield return new WaitForSeconds(0.3f);
            int lines = (AudioDirector.Instance != null ? AudioDirector.Instance.VoiceLines : 0) - voices;
            Log($"{label}: sees={sees} exposure={g.Exposure:0.00} distance={g.Distance:0.0} m light={Perception.IlluminationOf(Me):0.00} (×{g.Light:0.00}) factor={SensorMath.VisionFactor(g):0.00} → " +
                $"alerted={alerted} after {toAlert:0.0} s, warning {warning:0.0} s, then {state}; voice lines {lines}");

            if (lightsOff) yield return Shot(label);
            foreach (var l in lamps) { l.enabled = true; l.Lamp.enabled = true; }
            DevFlags.FreezeRobots = true;
            Release();
            Session.Despawn(robot.gameObject);
            if (Me.IsServer) Me.ResetForLevelServer();
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The player walks (or sneaks) back and forth 5 m from a blind listener for a few seconds.</summary>
        IEnumerator HearingCase(Vector3 at, bool crouch, string label)
        {
            var gm = GameManager.Instance;
            DevFlags.FreezeRobots = true;
            var robotPos = at + Vector3.forward * 5f;
            var robot = gm.SpawnRobotServer(RobotKind.Listener, robotPos, Quaternion.LookRotation(Vector3.forward)).GetComponent<RobotController>();
            Me.TeleportOwner(at + Vector3.up * 0.05f, Quaternion.LookRotation(Vector3.right));
            Drive(Vector2.zero, crouch: crouch);
            yield return new WaitForSeconds(0.8f);
            robot.Brain?.Reset(Time.time); // forget the teleport landing

            DevFlags.FreezeRobots = false;
            float t0 = Time.time, max = 0f, investigateAt = -1f;
            string states = robot.State.ToString();
            while (Time.time - t0 < 6f)
            {
                Drive(new Vector2(0, Mathf.Sin((Time.time - t0) * 1.6f) > 0 ? 1 : -1), crouch: crouch);
                max = Mathf.Max(max, robot.Suspicion);
                var s = robot.State.ToString();
                if (!states.EndsWith(s))
                {
                    states += " → " + s;
                    Log($"  {label} t={Time.time - t0:0.0}: {s}, suspicion {robot.Suspicion:0.00}, looks at {robot.Brain.LookPoint}, " +
                        $"player at {Me.transform.position} crouching={Me.Crouching}, robot at {robot.transform.position}");
                }
                if (robot.State == RobotState.Investigate && investigateAt < 0f) investigateAt = Time.time - t0;
                if (robot.State is RobotState.Alerted or RobotState.Chase) break;
                yield return null;
            }
            Release();
            Log($"{label}: max suspicion {max:0.00}, states {states}" + (investigateAt >= 0f ? $", went to check after {investigateAt:0.0} s" : ""));
            DevFlags.FreezeRobots = true;
            Session.Despawn(robot.gameObject);
            if (Me.IsServer) Me.ResetForLevelServer();
            yield return new WaitForSeconds(0.5f);
        }
    }
}
