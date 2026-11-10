using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Stealth.Enemies;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        partial void UiScenarioExtraImpl(List<IEnumerator> steps)
        {
            steps.Add(StashShots());
            steps.Add(NoiseMeterShots());
            steps.Add(QuotaShots());
            steps.Add(BannerAndAlarmShots());
            steps.Add(DetectionIndicatorCheck());
        }

        /// <summary>The team stash above the hatch: items go in from the backpack and get sold from the stash.</summary>
        IEnumerator StashShots()
        {
            var gm = GameManager.Instance;
            var inv = Me.GetComponent<PlayerInventory>();
            if (!Me.IsServer) yield break;
            inv.GiveServer(1, 3);
            inv.GiveServer(0, 2);
            var hatch = ActiveLevel.Layout.HatchPosition;
            Me.TeleportOwner(hatch + new Vector3(0f, 0.05f, -2.6f), Quaternion.LookRotation(Vector3.forward));
            Me.Motor.SetPitch(-8f);
            yield return new WaitForSeconds(0.5f);
            int before = inv.Items.Count;
            inv.RequestStash(0, 0, 0, inv.Items[0].Rot);
            yield return new WaitForSeconds(0.3f);
            gm.Stash.Add(3, 4, 2, 2, 0);
            yield return new WaitForSeconds(0.3f);
            Log($"stash: {gm.Stash.Items.Count} items worth {gm.Stash.TotalValue}, backpack {before} -> {inv.Items.Count}, board visible={UI.StashBoard.Instance != null && UI.StashBoard.Instance.Visible}");
            yield return Shot("stash_board");
            DevFlags.ForceHandsMode = true;
            yield return new WaitForSeconds(0.4f);
            yield return Shot("stash_board_hands");
            int collected = gm.Collected;
            inv.RequestSellStashed(gm.Stash.Items[0]);
            yield return new WaitForSeconds(0.3f);
            Log($"sold from stash: collected {collected} -> {gm.Collected}, stash {gm.Stash.Items.Count}");
            var audio = Audio.AudioDirector.Instance;
            Log(audio == null ? "audio: no director" : $"audio: music {audio.Track}, one-shots so far {audio.Played}, listener volume {AudioListener.volume:0.00}");
            DevFlags.ForceHandsMode = false;
        }

        /// <summary>Stand in a guard's view, off-centre, and capture the indicator while it fills up.</summary>
        IEnumerator DetectionIndicatorCheck()
        {
            var guard = RobotController.Robots.FirstOrDefault(r => r.Senses.HasEyes);
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
            var aw = guard.GetComponent<Detector>();
            float t = Time.time;
            while (Time.time - t < 4f && aw.AwarenessOf(Me.ClientId) < 0.4f) yield return null;
            Log($"detection: awareness {aw.AwarenessOf(Me.ClientId):0.00} after {Time.time - t:0.0}s");
            yield return Shot("noticed");
            while (Time.time - t < 8f && aw.AwarenessOf(Me.ClientId) < 0.99f) yield return null;
            Log($"detection: awareness {aw.AwarenessOf(Me.ClientId):0.00}, state {guard.State}");
            yield return Shot("detected");
            var cam = Me.Camera.transform;
            foreach (var d in Detector.All)
                Log($"  detector {d.name} pos={d.Position} aw={d.AwarenessOf(Me.ClientId):0.00} angle={UI.UiMath.ScreenAngle(cam.position, cam.forward, d.Position):0} guard={(d.gameObject == guard.gameObject)}");
            Log($"  me={Me.transform.position} fwd={cam.forward}");
        }

        IEnumerator NoiseMeterShots()
        {
            var c = ActiveLevel.Layout.HatchPosition + new Vector3(-3f, 0.05f, -3f);
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

        IEnumerator QuotaShots()
        {
            var gm = GameManager.Instance;
            if (!Me.IsServer) yield break;
            gm.AddCollectedServer(Mathf.Max(5, gm.Quota - gm.Collected)); // fill the quota
            yield return new WaitForSeconds(0.3f);
            yield return Shot("quota_gain");
            // Walk away from the hatch, facing away: the marker must sit on the screen edge.
            var hatch = ActiveLevel.Layout.HatchPosition;
            Me.TeleportOwner(hatch + new Vector3(3.5f, 0.05f, 3.5f), Quaternion.LookRotation(new Vector3(1, 0, 0.3f)));
            Me.Motor.SetPitch(0);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("hatch_marker_edge");
        }

        /// <summary>Coop: names over heads nearby, then the host goes down and the client sees the marker through walls.</summary>
        IEnumerator CoopTeamShots()
        {
            float t0 = Time.time;
            while (PlayerController.All.Count < 2 && Time.time - t0 < 20f) yield return null;
            var hatch = ActiveLevel.Layout.HatchPosition;
            if (Me.IsServer)
            {
                Me.TeleportOwner(hatch + new Vector3(-2f, 0.05f, 0f), Quaternion.LookRotation(Vector3.right));
                yield return new WaitForSeconds(5f);
                Me.HitServer(Me.transform.position + Vector3.right);
                yield return new WaitForSeconds(1.7f); // after the invulnerability window
                Me.HitServer(Me.transform.position + Vector3.right);
                Log($"host downed={Me.Downed}");
                yield return new WaitForSeconds(6f);
            }
            else
            {
                Me.TeleportOwner(hatch + new Vector3(3f, 0.05f, 0f), Quaternion.LookRotation(Vector3.left));
                Me.Motor.SetPitch(5f);
                yield return new WaitForSeconds(3f);
                yield return Shot("teammate_name");
                // Go to a neighbouring room so a wall is between us.
                var other = ActiveLevel.Layout.PatrolPoints.OrderBy(p => Vector3.Distance(p, hatch)).Skip(1).First();
                Me.TeleportOwner(other + new Vector3(0, 0.05f, 0), Quaternion.LookRotation(Flat(hatch - other)));
                yield return new WaitForSeconds(5f);
                var host = PlayerController.All.FirstOrDefault(p => p != Me);
                Log($"teammate: {host?.Nick} downed={host?.Downed}");
                yield return Shot("downed_marker");
            }
        }

        IEnumerator BannerAndAlarmShots()
        {
            UI.GameHUD.Instance.ShowBanner("Первое сообщение");
            UI.GameHUD.Instance.ShowBanner("Второе сообщение ждёт своей очереди");
            if (Me.IsServer) GameManager.Instance.RaiseAlarmServer(Me.transform.position + Vector3.forward * 50f, 1f);
            yield return new WaitForSeconds(1f);
            yield return Shot("banner_alarm");
            yield return new WaitForSeconds(3.2f);
            yield return Shot("banner_second");
        }
    }
}
