using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        partial void UiScenarioExtraImpl(List<IEnumerator> steps)
        {
            steps.Add(NoiseMeterShots());
            steps.Add(DetectionIndicatorCheck());
        }

        /// <summary>Stand in a guard's view, off-centre, and capture the indicator while it fills up.</summary>
        IEnumerator DetectionIndicatorCheck()
        {
            var guard = RobotController.All.FirstOrDefault(r => r.HasEyes);
            if (guard == null) { Log("detection: no guard on this level"); yield break; }
            var gp = guard.transform.position;
            // Stay in the guard's room so nothing blocks the view, at the far side in front of it.
            float dist = 7f;
            if (Physics.Raycast(gp + Vector3.up * 1.5f, guard.transform.forward, out var wall, 9f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                dist = Mathf.Clamp(wall.distance - 1f, 2.5f, 7f);
            var spot = gp + guard.transform.forward * dist;
            spot.y = gp.y + 0.05f;
            // Look 50 degrees to the left of the robot so the arc is clearly off-centre.
            var toRobot = Flat(gp - spot);
            Me.TeleportOwner(spot, Quaternion.AngleAxis(-50f, Vector3.up) * Quaternion.LookRotation(toRobot));
            Me.Motor.SetPitch(0f);
            var aw = guard.GetComponent<DetectorAwareness>();
            float t = Time.time;
            while (Time.time - t < 4f && aw.AwarenessOf(Me.OwnerClientId) < 0.4f) yield return null;
            Log($"detection: awareness {aw.AwarenessOf(Me.OwnerClientId):0.00} after {Time.time - t:0.0}s");
            yield return Shot("noticed");
            while (Time.time - t < 8f && aw.AwarenessOf(Me.OwnerClientId) < 0.99f) yield return null;
            Log($"detection: awareness {aw.AwarenessOf(Me.OwnerClientId):0.00}, state {guard.State.Value}");
            yield return Shot("detected");
            var cam = Me.Camera.transform;
            foreach (var d in DetectorAwareness.All)
                Log($"  detector {d.name} pos={d.Position} aw={d.AwarenessOf(Me.OwnerClientId):0.00} angle={UI.UiMath.ScreenAngle(cam.position, cam.forward, d.Position):0} guard={(d.gameObject == guard.gameObject)}");
            Log($"  me={Me.transform.position} fwd={cam.forward}");
        }

        IEnumerator NoiseMeterShots()
        {
            var c = LevelGenerator.Current.HatchPosition + new Vector3(-3f, 0.05f, -3f);
            Me.TeleportOwner(c, Quaternion.LookRotation(Vector3.forward));
            Drive(Vector2.up, crouch: true);
            yield return new WaitForSeconds(1.3f);
            yield return Shot("noise_crouch");
            Me.TeleportOwner(c, Quaternion.LookRotation(Vector3.forward));
            Drive(Vector2.up, sprint: true);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("noise_sprint");
            Release();
        }
    }
}
