using System;
using System.Collections;
using Stealth.Enemies;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Every robot / device kind spawned in front of the camera in the lobby, one screenshot each.
        /// -kinds Guard,Drone limits the list. Robots are peaceful unless -hostile is passed.
        /// </summary>
        IEnumerator GalleryScenario()
        {
            var gm = GameManager.Instance;
            if (!Me.IsServer) yield break;
            var only = Arg("-kinds");
            DevFlags.FreezeRobots = true;
            DevFlags.SkipGracePeriod = true; // sensors on immediately
            DevFlags.HideStashBoard = true;  // the stage is right behind the hatch
            var hatch = ActiveLevel.Layout.HatchPosition;
            // Clear the stage: the level's own guards (robots, turrets, sensors, lasers) would photobomb the gallery
            // and hit the player during the reaction checks (a knockback looks like running to a motion sensor).
            foreach (var d in Detector.All.ToArray()) Session.Despawn(d.gameObject);
            foreach (var gate in FindObjectsByType<LaserGate>()) Session.Despawn(gate.gameObject);

            foreach (RobotKind kind in Enum.GetValues(typeof(RobotKind)))
            {
                if (!string.IsNullOrEmpty(only) && Array.IndexOf(only.Split(','), kind.ToString()) < 0) continue;
                var stage = hatch + new Vector3(0, 0, 2.6f);
                var pos = stage;
                var rot = Quaternion.LookRotation(Vector3.back);
                bool onWall = kind == RobotKind.MotionSensor || kind == RobotKind.Camera;
                if (onWall) { pos = hatch + new Vector3(0, kind == RobotKind.Camera ? 2.6f : 2.2f, 4.75f); stage = pos; stage.y = hatch.y; }
                var obj = gm.SpawnRobotServer(kind, pos, rot);
                if (obj == null) { Log($"gallery: {kind} has no prefab yet"); continue; }
                var view = hatch + new Vector3(0.6f, 0.05f, -1.6f);
                // Robots: aim at the body. Small devices without much of a body: at the sensor itself (its indicator point).
                bool small = RobotRoster.IsDevice(kind) || kind == RobotKind.Camera;
                var aim = small ? obj.GetComponent<Detector>().Position : stage + Vector3.up * 0.9f;
                LookAtFrom(new Pose(view, Quaternion.LookRotation(Flat(stage - view))), aim);
                yield return new WaitForSeconds(1.5f);
                // Some robots move to their place on spawn (walls, ceiling): look at where they ended up.
                var at = obj.transform.position;
                if ((at - pos).sqrMagnitude > 0.25f)
                    LookAtFrom(ViewPoint(at, 3.5f), obj.GetComponent<Detector>().Position);
                yield return new WaitForSeconds(0.6f);
                Log($"gallery: {kind} at {obj.transform.position}, look prompt='{UI.GameHUD.Instance.CurrentPrompt.Text}'");
                yield return Shot($"kind_{kind}");
                yield return GalleryExtra(kind, obj);
                Session.Despawn(obj);
                yield return null;
            }
        }

        /// <summary>Per-kind behaviour checks hooked in by later iterations.</summary>
        IEnumerator GalleryExtra(RobotKind kind, GameObject obj)
        {
            var steps = new System.Collections.Generic.List<IEnumerator>();
            GalleryExtraImpl(kind, obj, steps);
            foreach (var s in steps) yield return s;
        }

        partial void GalleryExtraImpl(RobotKind kind, GameObject obj, System.Collections.Generic.List<IEnumerator> steps);
    }
}
