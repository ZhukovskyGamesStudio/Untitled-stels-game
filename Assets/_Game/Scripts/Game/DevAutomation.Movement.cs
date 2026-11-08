using System.Collections;
using System.Collections.Generic;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        partial void MoveScenarioExtraImpl(List<IEnumerator> steps)
        {
            steps.Add(JumpCheck());
        }

        IEnumerator JumpCheck()
        {
            Release();
            yield return new WaitForSeconds(0.4f);
            float y0 = Me.transform.position.y, peak = y0;
            PlayerController.Autopilot = new PlayerInputState { Jump = true };
            yield return null;
            Release();
            float t = Time.time;
            bool landed = false;
            while (Time.time - t < 1.5f)
            {
                peak = Mathf.Max(peak, Me.transform.position.y);
                if (Time.time - t > 0.2f && Me.Motor.Grounded) { landed = true; break; }
                yield return null;
            }
            Log($"jump: height {peak - y0:0.00} m, landed={landed} after {Time.time - t:0.00}s");

            // Buffered jump: press shortly before landing, expect a second jump.
            PlayerController.Autopilot = new PlayerInputState { Jump = true };
            yield return null;
            Release();
            t = Time.time;
            while (Time.time - t < 2f && !(Me.Motor.Velocity.y < -1f && Me.transform.position.y - y0 < 0.25f)) yield return null;
            PlayerController.Autopilot = new PlayerInputState { Jump = true };
            yield return null;
            Release();
            float peak2 = Me.transform.position.y;
            t = Time.time;
            while (Time.time - t < 1f) { peak2 = Mathf.Max(peak2, Me.transform.position.y); yield return null; }
            Log($"buffered jump: second jump height {peak2 - y0:0.00} m");
            yield return Shot("after_jumps");
        }
    }
}
