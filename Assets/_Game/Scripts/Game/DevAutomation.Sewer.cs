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
            LookFrom(c + new Vector3(0f, 0.05f, 3.2f), c + Vector3.up * 1f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("01_car_side");
            LookFrom(c - car.transform.forward * 4.5f + Vector3.up * 0.05f + car.transform.right * 1.2f, c + Vector3.up * 1.2f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02_car_back");

            // what the team sees ahead from the car, and the hall with its tunnels
            LookFrom(c + car.transform.right * 1.6f, c + car.transform.forward * 30f + Vector3.up * 1.4f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02a_ahead");
            var hall = sewer.transform.TransformPoint(new Vector3(plan.HubX + 3f, 0.1f, 0f));
            LookFrom(hall, sewer.transform.TransformPoint(new Vector3(plan.End, 1.4f, 0f)));
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02b_hall");
            // the panels over the ways on: in front of the end wall, facing the hall, with their text
            foreach (var b in sewer.Branches)
                if (b.Sign != null)
                    Log($"sign {b.Sign.name}: shown {b.Sign.gameObject.activeInHierarchy}, at {sewer.transform.InverseTransformPoint(b.Sign.transform.position)}, " +
                        $"faces {sewer.transform.InverseTransformDirection(-b.Sign.transform.forward)}, '{b.Sign.Title.text}' / '{b.Sign.Body.text}'");
            LookFrom(sewer.transform.TransformPoint(new Vector3(plan.End - 7f, 0.1f, 0f)), sewer.transform.TransformPoint(new Vector3(plan.End, 4.2f, 0f)));
            yield return new WaitForSeconds(0.6f);
            yield return Shot("02bb_signs");
            // the closed start of the tunnel behind the car, near and from a few metres, and the tunnel's end from the hall
            var capFace = sewer.transform.TransformPoint(new Vector3(0f, 1.3f, 0f));
            LookFrom(sewer.transform.TransformPoint(new Vector3(2.2f, 0.1f, 1.2f)), capFace);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02c_cap_near");
            LookFrom(sewer.transform.TransformPoint(new Vector3(5.5f, 0.1f, -1.5f)), capFace);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02d_cap_far");
            LookFrom(sewer.transform.TransformPoint(new Vector3(plan.HubX + 6f, 0.1f, -2f)), sewer.transform.TransformPoint(new Vector3(plan.HubX - 4f, 1.5f, 0f)));
            yield return new WaitForSeconds(0.4f);
            yield return Shot("02e_hall_back");
            // the whole sewer from above (the vaults are one-sided: seen from outside they are not drawn), and its pieces' bounds
            foreach (Transform piece in sewer.transform)
            {
                var rs = piece.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0 || piece.name.StartsWith("Junk") || piece.name.Contains("Lamp")) continue;
                var bounds = rs[0].bounds;
                foreach (var r in rs) bounds.Encapsulate(r.bounds);
                Log($"piece {piece.name}: x {bounds.min.x:0.0}..{bounds.max.x:0.0} z {bounds.min.z:0.0}..{bounds.max.z:0.0} y {bounds.min.y:0.0}..{bounds.max.y:0.0}");
            }
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            Me.TeleportOwner(sewer.transform.TransformPoint(new Vector3(plan.End * 0.6f, 30f, 0f)), Quaternion.LookRotation(Vector3.right));
            Me.Motor.SetPitch(89f);
            yield return new WaitForSeconds(0.5f);
            RenderSettings.fog = false;
            yield return Shot("02f_from_above");
            RenderSettings.fog = fog;
            Log($"hall: ways {gm.RouteOptions.Count}, bars {string.Join(",", sewer.Branches.Select(b => b.Grate != null && b.Grate.activeSelf ? "x" : "o"))}");

            // the bed: in with E, out with E (a real key press, as a player does it)
            Me.TeleportOwner(car.ExitPoint(2), car.transform.rotation);
            yield return new WaitForSeconds(0.3f);
            gm.UseServer(Me, UseKind.CarSeat, 2);
            yield return new WaitForSeconds(0.5f);
            int inBed = car.SeatOf(Me.ClientId);
            yield return PressKey(UnityEngine.InputSystem.Key.E);
            yield return new WaitForSeconds(0.5f);
            Log($"bed seat: in {inBed}, after E {car.SeatOf(Me.ClientId)}, me at {Me.transform.position}");

            // the run's start, nothing fed into the engine yet: E at the driver's door, W held — the car must go
            var startPose = new Pose(car.transform.position, car.transform.rotation);
            LookFrom(car.ExitPoint(0), car.Seats[0].position + Vector3.up * 0.4f);
            yield return new WaitForSeconds(0.4f);
            yield return PressKey(UnityEngine.InputSystem.Key.E);
            yield return new WaitForSeconds(0.4f);
            int sat = car.SeatOf(Me.ClientId);
            yield return Shot("02g_start_full_tank");
            yield return HoldKeys(1.5f, UnityEngine.InputSystem.Key.W);
            float went = Vector3.Distance(car.transform.position, startPose.position);
            Log($"first drive: fuel {gm.Collected}/{gm.Quota}, seat {sat}, W for 1.5 s -> {went:0.0} m {(went > 2f ? "GOES" : "FAIL: the car does not go")}");
            yield return new WaitForSeconds(0.5f);
            yield return PressKey(UnityEngine.InputSystem.Key.E);
            yield return new WaitForSeconds(0.4f);
            car.Place(startPose.position, startPose.rotation);
            yield return new WaitForSeconds(0.3f);

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
            LookFrom(car.Bed.position - car.transform.forward * 1.8f + car.transform.right * 1.4f + Vector3.up * 0.4f, car.Bed.TransformPoint(new Vector3(0, 0.2f, 1f)));
            Me.Motor.SetPitch(35f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("03_bed");

            // into the driver's seat and along the tunnel into the way the route offers first
            // at the driver's door, facing the cab: the reticle must offer the wheel, and E (a real key press) seats us there
            var door = car.ExitPoint(0);
            LookFrom(door, car.Seats[0].position + Vector3.up * 0.4f);
            yield return new WaitForSeconds(0.4f);
            Log($"at the door: target {(Me.UseTarget != null ? Me.UseTarget.name : "none")}, prompt '{Me.UsePrompt}'");
            float seatX = car.transform.InverseTransformPoint(car.Seats[0].position).x;
            float wheelX = car.Steering != null ? car.transform.InverseTransformPoint(car.Steering.position).x : float.NaN;
            Log($"driver side: seat {seatX:0.00}, steering wheel {wheelX:0.00} → {(seatX < 0f && wheelX < 0f ? "LEFT" : "RIGHT")}-hand drive");
            yield return Shot("04_00_at_driver_door");
            yield return PressKey(UnityEngine.InputSystem.Key.E);
            yield return new WaitForSeconds(0.6f);
            if (car.SeatOf(Me.ClientId) != 0) Log($"FAIL: E at the driver's door seats us in {car.SeatOf(Me.ClientId)}");
            if (car.SeatOf(Me.ClientId) != 0) { gm.UseServer(Me, UseKind.CarSeat, 0); yield return new WaitForSeconds(0.6f); }
            // a passenger rides along (the drive scene shows the team in the car): a bot in the passenger seat if there is a second player
            foreach (var other in PlayerController.All) if (other != Me && car.SeatOf(other.ClientId) < 0) { gm.UseServer(other, UseKind.CarSeat, 1); break; }
            Log($"seat: {car.SeatOf(Me.ClientId)}, driver {car.Driver}, engine {car.EngineOn}, me at {Me.transform.position}");
            yield return Shot("04_driver_seat");
            // into the tunnel's wall at full throttle, steering into it: the car must stay inside
            var parked = new Pose(car.transform.position, car.transform.rotation);
            float tw0 = Time.time, worst = 0f;
            while (Time.time - tw0 < 4f)
            {
                // a short turn, then straight on into the wall at an angle (and grinding along it, steering into it)
                float t = Time.time - tw0;
                DevFlags.CarInput = new CarInput { Throttle = 1f, Steer = t < 0.7f ? 1f : t > 2.5f ? 1f : 0f };
                worst = Mathf.Max(worst, Mathf.Abs(sewer.transform.InverseTransformPoint(car.transform.position).z));
                yield return null;
            }
            DevFlags.CarInput = new CarInput { Brake = true };
            yield return new WaitForSeconds(0.3f);
            yield return Shot("04a_car_at_wall"); // from the driver's seat
            Log($"wall: farthest from the tunnel's axis {worst:0.00} m (walls at {SewerPlan.WalkwayZ + 0.5f:0.0}) -> {(worst < SewerPlan.WalkwayZ + 0.5f ? "INSIDE" : "FAIL: through the wall")}");
            car.Place(parked.position, parked.rotation);
            yield return new WaitForSeconds(0.3f);
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
            yield return Shot("05b_before_departure"); // the last frame of the sewer, from the driver's seat
            while (gm.DriveUntil <= 0 && gm.Phase == RunPhase.Sewer && Time.time - tw < 10f) yield return null;
            DevFlags.CarInput = null;
            // the drive scene glides out of the cab to the side view: frames along the way
            while (DriveScene.Active == null && Time.time - tw < 12f) yield return null;
            var drive = DriveScene.Active;
            float g0 = Time.time;
            yield return Shot("06a_glide_start");
            foreach (float at in new[] { 0.6f, 1.3f, 2.0f, drive != null ? drive.GlideTime + 0.4f : 3f })
            {
                while (Time.time - g0 < at) yield return null;
                Log($"glide at {at:0.0} s: {(drive != null ? drive.Glide : -1f):0.00}, fov {(drive != null ? drive.Camera.fieldOfView : 0f):0}");
                yield return Shot($"06_glide_{Mathf.RoundToInt(at * 10f):00}"); // tenths of a second: no locale comma in the name
            }
            Log($"drive scene: until {gm.DriveUntil - Session.ServerTime:0.0} s, shown {DriveScene.Active != null}");
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
            LookFrom(Me.transform.position, sewer.Gate.transform.position + Vector3.up * 1.8f);
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
