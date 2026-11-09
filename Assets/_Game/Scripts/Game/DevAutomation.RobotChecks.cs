using System.Collections;
using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Level;
using Unity.Netcode;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        partial void GalleryExtraImpl(RobotKind kind, NetworkObject obj, List<IEnumerator> steps)
        {
            if (Arg("-hostile") == null) return;
            if (kind == RobotKind.Thumper) steps.Add(SneakCheck(kind, obj, crouch: true));
            if (kind == RobotKind.Hound) { steps.Add(TrailCheck(obj)); return; }
            steps.Add(DetectionCheck(kind, obj));
        }

        /// <summary>
        /// The robot is switched on, the player stands in front of it (walking in place to make some noise and
        /// vibration) and we log how it reacts: awareness, alarm, damage.
        /// </summary>
        IEnumerator DetectionCheck(RobotKind kind, NetworkObject obj)
        {
            var gm = GameManager.Instance;
            var aw = obj.GetComponent<DetectorAwareness>();
            int hp0 = Me.Hp.Value;
            gm.AlarmUntil.Value = 0;
            var p = obj.transform.position;
            var front = obj.transform.forward;
            var stand = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)) + 0.05f, p.z) + Flat(front).normalized * 3.5f;
            Me.TeleportOwner(stand, Quaternion.LookRotation(Flat(p - stand)));
            yield return CustomDetectionSetup(kind, obj);
            GameManager.FreezeRobots = false;
            float t0 = Time.time, aware = 0f, firstAware = -1f;
            while (Time.time - t0 < 6f)
            {
                // Shuffle back and forth: footsteps for microphones / seismic sensors, movement for PIR.
                Drive(new Vector2(Mathf.Sin(Time.time * 3f) > 0 ? 1 : -1, 0));
                float a = aw != null ? aw.AwarenessOf(Me.OwnerClientId) : 0f;
                aware = Mathf.Max(aware, a);
                if (firstAware < 0 && a > 0.05f) firstAware = Time.time - t0;
                if (a >= 0.99f && (gm.AlarmActive || Me.Hp.Value < hp0)) break;
                yield return null;
            }
            Release();
            yield return Shot($"kind_{kind}_reacting");
            Log($"detect {kind}: noticed after {firstAware:0.0}s, max awareness {aware:0.00}, alarm={gm.AlarmActive}, hp {hp0}->{Me.Hp.Value}");
            GameManager.FreezeRobots = true;
            if (Me.Hp.Value < hp0 && Me.IsServer) Me.ResetForLevelServer();
            yield return new WaitForSeconds(0.3f);
        }

        partial void CustomDetectionSetupImpl(RobotKind kind, NetworkObject obj, List<IEnumerator> steps);

        IEnumerator CustomDetectionSetup(RobotKind kind, NetworkObject obj)
        {
            var steps = new List<IEnumerator>();
            CustomDetectionSetupImpl(kind, obj, steps);
            foreach (var s in steps) yield return s;
        }

        /// <summary>Moves near the robot in a specific way (e.g. crouching) and logs whether it notices.</summary>
        IEnumerator SneakCheck(RobotKind kind, NetworkObject obj, bool crouch)
        {
            var aw = obj.GetComponent<DetectorAwareness>();
            var p = obj.transform.position;
            var stand = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)) + 0.05f, p.z) + Flat(obj.transform.right).normalized * 3.5f;
            Me.TeleportOwner(stand, Quaternion.LookRotation(Flat(obj.transform.forward)));
            GameManager.FreezeRobots = false;
            float t0 = Time.time, max = 0f;
            while (Time.time - t0 < 4f)
            {
                Drive(new Vector2(0, Mathf.Sin(Time.time * 2f) > 0 ? 1 : -1), crouch: crouch);
                max = Mathf.Max(max, aw.AwarenessOf(Me.OwnerClientId));
                yield return null;
            }
            Release();
            GameManager.FreezeRobots = true;
            Log($"sneak past {kind} (crouch={crouch}): max awareness {max:0.00}");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The player walks past the hound's nose and away out of the room; the hound must follow the trail.</summary>
        IEnumerator TrailCheck(NetworkObject obj)
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
            GameManager.FreezeRobots = false;
            float t0 = Time.time;
            while (Time.time - t0 < 8f && robot.State.Value != RobotState.Chase) yield return null;
            float tChase = Time.time - t0;
            yield return new WaitForSeconds(2f);
            float after = Vector3.Distance(Flat(obj.transform.position), Flat(Me.transform.position));
            yield return Shot("kind_Hound_tracking");
            Log($"hound: started tracking after {tChase:0.0}s, state={robot.State.Value}, distance {before:0.0} -> {after:0.0} m");
            GameManager.FreezeRobots = true;
            if (Me.IsServer) Me.ResetForLevelServer();
        }
    }
}
