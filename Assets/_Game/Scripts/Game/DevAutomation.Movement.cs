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
            steps.Add(StepDownCheck());
            steps.Add(StaminaCheck());
            steps.Add(CrouchToggleCheck());
            steps.Add(KnockbackCheck());
            steps.Add(SettingsShot());
        }

        IEnumerator JumpCheck()
        {
            Release();
            yield return new WaitForSeconds(0.4f);
            float y0 = Me.transform.position.y, peak = y0;
            DevFlags.Autopilot = new PlayerInputState { Jump = true };
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
            DevFlags.Autopilot = new PlayerInputState { Jump = true };
            yield return null;
            Release();
            t = Time.time;
            while (Time.time - t < 2f && !(Me.Motor.Velocity.y < -1f && Me.transform.position.y - y0 < 0.25f)) yield return null;
            DevFlags.Autopilot = new PlayerInputState { Jump = true };
            yield return null;
            Release();
            float peak2 = Me.transform.position.y;
            t = Time.time;
            while (Time.time - t < 1f) { peak2 = Mathf.Max(peak2, Me.transform.position.y); yield return null; }
            Log($"buffered jump: second jump height {peak2 - y0:0.00} m");
            yield return Shot("after_jumps");
        }

        /// <summary>Builds a few 0.25 m steps locally and walks down them: the player must stay grounded.</summary>
        IEnumerator StepDownCheck()
        {
            var basePos = Level.ActiveLevel.Layout.HatchPosition + new Vector3(-4f, 0f, -2.5f);
            var root = new GameObject("TestSteps");
            for (int i = 0; i < 4; i++)
            {
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.transform.SetParent(root.transform);
                float h = 1f - i * 0.25f;
                step.transform.position = basePos + new Vector3(i * 0.5f, h / 2f, 0);
                step.transform.localScale = new Vector3(0.5f, h, 1.4f);
            }
            Me.TeleportOwner(basePos + new Vector3(0, 1.05f, 0), Quaternion.LookRotation(Vector3.right));
            Release();
            yield return new WaitForSeconds(0.5f);
            int snaps0 = Me.Motor.SnapCount, airFrames = 0, frames = 0;
            float landed = 0;
            System.Action<float> onLand = v => landed = Mathf.Max(landed, v);
            Me.Motor.Landed += onLand;
            Drive(Vector2.up);
            float t = Time.time;
            while (Time.time - t < 1.1f)
            {
                frames++;
                if (!Me.Motor.Grounded) airFrames++;
                yield return null;
            }
            Release();
            Me.Motor.Landed -= onLand;
            Log($"steps down: snaps={Me.Motor.SnapCount - snaps0} airborne {airFrames}/{frames} frames, hardest landing {landed:0.0} m/s, y={Me.transform.position.y:0.00}");
            yield return Shot("steps");
            Object.Destroy(root);
        }

        IEnumerator StaminaCheck()
        {
            Me.TeleportOwner(Level.ActiveLevel.Layout.HatchPosition + new Vector3(0, 0.05f, -2.5f), Quaternion.LookRotation(Vector3.right));
            Me.Motor.Stamina.Refill();
            yield return new WaitForSeconds(0.3f);
            float t = Time.time;
            bool shot = false;
            while (Time.time - t < 9f && !Me.Motor.Stamina.Exhausted)
            {
                // Run in a circle around the hatch.
                Drive(Vector2.up, sprint: true, look: new Vector2(600f * Time.deltaTime, 0));
                if (!shot && Time.time - t > 3f) { shot = true; yield return Shot("stamina"); }
                yield return null;
            }
            Release();
            Log($"stamina: exhausted after {Time.time - t:0.0}s of sprint, sprinting now={Me.Motor.IsSprinting}");
        }

        IEnumerator CrouchToggleCheck()
        {
            Release();
            yield return new WaitForSeconds(0.3f);
            DevFlags.Autopilot = new PlayerInputState { CrouchToggle = true };
            yield return null;
            Release();
            yield return new WaitForSeconds(0.4f);
            bool crouchedAfterToggle = Me.Motor.IsCrouching;
            Drive(Vector2.up, sprint: true);
            yield return new WaitForSeconds(0.3f);
            bool crouchedWhileSprinting = Me.Motor.IsCrouching;
            Release();
            Log($"crouch toggle: crouched={crouchedAfterToggle}, sprint cancels it={!crouchedWhileSprinting}");
        }

        IEnumerator KnockbackCheck()
        {
            var c = Level.ActiveLevel.Layout.HatchPosition + new Vector3(0, 0.05f, 2f);
            Me.TeleportOwner(c, Quaternion.LookRotation(Vector3.forward));
            Release();
            yield return new WaitForSeconds(0.5f);
            if (!Me.IsServer) yield break;
            var p0 = Me.transform.position;
            Me.HitServer(p0 + Vector3.forward * 1.2f); // hit from the front
            yield return new WaitForSeconds(0.05f);
            yield return Shot("hit_shake");
            yield return new WaitForSeconds(0.8f);
            var d = Me.transform.position - p0;
            Log($"knockback: moved {new Vector3(d.x, 0, d.z).magnitude:0.00} m backwards={d.z < -0.5f}, hp={Me.Hp.Value}");
        }

        /// <summary>Menu pages (the main menu is captured before hosting): pause, settings.</summary>
        IEnumerator MenuScenario()
        {
            UI.GameMenu.OpenPauseForTest();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("pause");
            yield return SettingsShot();
            Log($"menu: open={UI.GameMenu.IsOpen} settings={UI.GameMenu.SettingsOpen}");
        }

        IEnumerator SettingsShot()
        {
            UI.GameMenu.OpenSettingsForTest();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("settings");
        }
    }
}
