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
    }
}
