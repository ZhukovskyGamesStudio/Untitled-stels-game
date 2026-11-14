using System.Collections;
using System.Linq;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// A run from the start with the car (<c>Tools/dev.sh run sewer</c>): it starts in the sewer at the pickup — loot into
        /// the engine (fuel), a thing onto the bed, the driver's seat, a drive along the tunnel and into a way on, the
        /// drive scene while the house is built, the team in the entry hatch; in the house the exit hatch with a thing in
        /// its stash, down again — the gate shut, the lever, the gate open, the thing in the car's bed.
        /// </summary>
        IEnumerator SewerScenario()
        {
            DevFlags.FreezeRobots = true;
            DevFlags.Peaceful = true;
            var gm = GameManager.Instance;
            yield return new WaitForSeconds(1.5f);
            var sewer = SewerLocation.Active;
            var car = Car.Active;
            Log($"start: phase {gm.Phase}, sewer {sewer != null}, car {car != null}, ways {string.Join(",", gm.RouteOptions)}, fuel {gm.Collected}/{gm.Quota}, gate open {gm.GateOpen}");
            if (gm.Phase != RunPhase.Sewer || sewer == null || car == null) { Log("FAIL: the run does not start in the sewer at the car"); yield break; }
            var plan = sewer.Plan;
            Log($"plan: {plan.Segments.Count} segments ({string.Join("", plan.Segments.Select(s => s.Variant))}), hub at {plan.HubX:0}, lamps {plan.LampColor}");

            // the car from the side and from behind
            var c = car.transform.position;
            LookAtFrom(new Pose(c + new Vector3(0f, 0.05f, 3.2f), Quaternion.identity), c + Vector3.up * 1f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("01_car_side");
            LookAtFrom(new Pose(c - car.transform.forward * 4.5f + Vector3.up * 0.05f + car.transform.right * 1.2f, Quaternion.identity), c + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02_car_back");

            // loot into the engine
            var inv = Me.GetComponent<PlayerInventory>();
            inv.GiveServer(4, 2);
            inv.GiveServer(1, 3);
            int before = gm.Collected;
            var hopper = car.GetComponentInChildren<CarHopper>().transform.position;
            Me.TeleportOwner(new Vector3(hopper.x, car.transform.position.y + 0.05f, hopper.z) + car.transform.forward * 1.6f, Quaternion.LookRotation(-car.transform.forward));
            yield return new WaitForSeconds(0.3f);
            gm.UseServer(Me, UseKind.CarHopper, 0);
            Log($"hopper: fuel {before} -> {gm.Collected}, backpack {inv.Items.Count}");
            // a thing onto the bed (dropped over it) and one taken back
            inv.GiveServer(2, 1);
            inv.GiveServer(5, 4);
            Me.TeleportOwner(car.Bed.position - car.transform.forward * 1.2f + car.transform.right * 1.6f - Vector3.up * 0.8f, Quaternion.LookRotation(-car.transform.right));
            yield return new WaitForSeconds(0.3f);
            var bed = car.Bed.TransformPoint(new Vector3(0.2f, 0.3f, 1.0f));
            inv.DropServer(0, bed, 30f);
            inv.DropServer(0, car.Bed.TransformPoint(new Vector3(-0.3f, 0.3f, 1.1f)), 80f);
            yield return new WaitForSeconds(0.3f);
            Log($"bed: {gm.Cargo.Count} things (value {gm.Cargo.Value}), backpack {inv.Items.Count}");
            LookAtFrom(new Pose(car.Bed.position - car.transform.forward * 1.8f + car.transform.right * 1.4f + Vector3.up * 0.4f, Quaternion.identity), car.Bed.TransformPoint(new Vector3(0, 0.2f, 1f)));
            Me.Motor.SetPitch(35f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("03_bed");

            // into the driver's seat and along the tunnel into the way the route offers first
            Me.TeleportOwner(car.ExitPoint(0), car.transform.rotation);
            yield return new WaitForSeconds(0.3f);
            gm.UseServer(Me, UseKind.CarSeat, 0);
            yield return new WaitForSeconds(0.6f);
            Log($"seat: {car.SeatOf(Me.ClientId)}, driver {car.Driver}, engine {car.EngineOn}, me at {Me.transform.position}");
            yield return Shot("04_driver_seat");
            int tunnel = SewerBranches.Slots(gm.RouteOptions.Count)[0];
            var mouth = sewer.Branches[tunnel].Mouth;
            var goal = mouth.position + mouth.right * 7f;
            float t0 = Time.time, topSpeed = 0f;
            bool shot = false;
            while (Time.time - t0 < 25f && sewer.BranchAt(car.transform.position) != tunnel)
            {
                // aim at the mouth first (lined up), then into the tunnel
                var aim = (car.transform.position - mouth.position).x < -6f ? mouth.position - mouth.right * 4f : goal;
                var to = Flat(aim - car.transform.position);
                float angle = Vector3.SignedAngle(Flat(car.transform.forward), to, Vector3.up);
                DevFlags.CarInput = new CarInput { Throttle = 0.7f, Steer = Mathf.Clamp(angle / 25f, -1f, 1f) };
                topSpeed = Mathf.Max(topSpeed, car.Speed);
                if (!shot && Time.time - t0 > 2.5f) { shot = true; yield return Shot("05_driving"); }
                yield return null;
            }
            DevFlags.CarInput = new CarInput { Brake = true };
            Log($"drive: in tunnel {sewer.BranchAt(car.transform.position)} after {Time.time - t0:0.0} s, top speed {topSpeed:0.0} m/s, at {car.transform.position}");
            yield return new WaitForSeconds(1f);
            Log($"countdown: lane {gm.ChosenLane}, go at {gm.GoAt - Session.ServerTime:0.0} s");
            float tw = Time.time;
            while (gm.DriveUntil <= 0 && gm.Phase == RunPhase.Sewer && Time.time - tw < 10f) yield return null;
            DevFlags.CarInput = null;
            yield return new WaitForSeconds(1.2f);
            Log($"drive scene: until {gm.DriveUntil - Session.ServerTime:0.0} s, shown {DriveScene.Active != null}");
            yield return Shot("06_drive_scene");
            tw = Time.time;
            while (gm.Phase != RunPhase.Playing && Time.time - tw < 15f) yield return null;
            yield return new WaitForSeconds(2f);
            var l = ActiveLevel.Layout;
            Log($"house: phase {gm.Phase}, house {gm.Level.Index}, fuel {gm.Collected}/{gm.Quota}, in hatch {Me.InHatch} ({Me.HatchIndex}), entry stash {gm.Stash.Items.Count}");
            if (gm.Phase != RunPhase.Playing || l == null) { Log("FAIL: the house did not start"); yield break; }
            yield return Shot("07_in_entry_hatch");

            // the exit hatch: a thing in its stash, everyone down
            Me.SetHatch(false);
            yield return new WaitForSeconds(0.5f);
            Me.TeleportOwner(l.ExitHatchPosition + new Vector3(1.2f, 0.05f, 0f), Quaternion.LookRotation(Vector3.left));
            yield return new WaitForSeconds(0.5f);
            Log($"exit hatch: {(l.ExitHatchPosition - l.HatchPosition).magnitude:0} m from the entry, near {Hatch.IndexNear(Me.transform.position)}");
            inv.GiveServer(3, 4);
            inv.StashAllServer();
            Log($"exit stash: {gm.ExitStash.Items.Count} things, backpack {inv.Items.Count}");
            yield return Shot("08_exit_hatch");
            Me.SetHatch(true);
            tw = Time.time;
            while (gm.Phase == RunPhase.Playing && Time.time - tw < 15f) yield return null;
            yield return new WaitForSeconds(2f);
            sewer = SewerLocation.Active;
            car = Car.Active;
            Log($"down: phase {gm.Phase}, car {car != null}, gate open {gm.GateOpen}, bed {gm.Cargo.Count}, at {Me.transform.position}");
            if (sewer == null || sewer.Gate == null) { Log("FAIL: no gate after a house"); yield break; }
            yield return Shot("09_down_at_exit");
            var lever = sewer.Gate.GetComponentInChildren<GateLever>();
            Me.TeleportOwner(lever.transform.position + new Vector3(1.2f, -1.2f, -1f), Quaternion.identity);
            LookAtFrom(new Pose(Me.transform.position, Quaternion.identity), sewer.Gate.transform.position + Vector3.up * 1.8f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("10_gate_shut");
            gm.UseServer(Me, UseKind.GateLever, 0);
            yield return new WaitForSeconds(2.6f);
            Log($"gate: open {gm.GateOpen}");
            yield return Shot("11_gate_open");
            Log("DONE");
        }
    }
}
