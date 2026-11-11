using System.Collections;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// A run from the start (<c>Tools/dev.sh run sewer 1</c>): it starts in the sewer — the cart with the stash, the
        /// tunnels with their panels; everyone walks into a tunnel and the house starts. In the house: the safe dial, a
        /// marked robot through a wall, a camera turning on its bracket, everyone hides in the hatch (peeking out), back
        /// in the sewer; then into a tunnel again without the quota — the water rises and the run ends.
        /// </summary>
        IEnumerator SewerScenario()
        {
            DevFlags.FreezeRobots = true;
            DevFlags.Peaceful = true;
            var gm = GameManager.Instance;
            float t0 = Time.time;
            bool expectClients = Me.IsServer && Arg("-autotest-linger") != null;
            while (expectClients && PlayerController.All.Count < 2 && Time.time - t0 < 25f) yield return null;
            yield return new WaitForSeconds(1.5f);
            int slot = PlayerLooks.SlotOf(Me.ClientId);
            var sewer = SewerLocation.Active;
            Log($"start: phase {gm.Phase}, in the sewer {sewer != null}, ways {string.Join(",", gm.RouteOptions)}, players {PlayerController.All.Count}");
            if (gm.Phase != RunPhase.Sewer || sewer == null) { Log("FAIL: the run does not start in the sewer"); yield break; }

            // F8 on a client switches the style of the whole team (the host sees it too).
            int styleBefore = VisualStyles.CurrentIndex;
            if (!Me.IsServer) gm.RequestStyle((Mathf.Max(0, styleBefore) + 3) % Mathf.Max(1, VisualStyles.All.Count));
            yield return new WaitForSeconds(1.5f);
            Log($"style: {styleBefore} -> {VisualStyles.CurrentIndex} ({VisualStyles.Current?.name}), team index {gm.StyleIndex}");
            if (!Me.IsServer && styleBefore >= 0) gm.RequestStyle(styleBefore); // back to the usual look for the screenshots
            yield return new WaitForSeconds(0.5f);

            if (Me.IsServer)
            {
                yield return Shot("01_sewer_start");
                // Some loot in the team stash: it lies in the cart.
                gm.Stash.Add(4, 4, 0, 0, 0);
                gm.Stash.Add(2, 3, 4, 0, 1);
                gm.Stash.Add(1, 2, 0, 2, 0);
                gm.Stash.Add(5, 1, 3, 2, 0);
                yield return new WaitForSeconds(0.5f);
                var cart = sewer.TradeSpot.position;
                var from = cart + new Vector3(1.4f, 0.05f, 1.6f);
                LookAtFrom(new Pose(from, Quaternion.LookRotation(Flat(cart - from))), cart + Vector3.up * 0.7f);
                yield return new WaitForSeconds(0.4f);
                yield return Shot("02_cart");
                // The hall and the panels over the tunnels.
                LookAtFrom(new Pose(new Vector3(-13.5f, 0.05f, 1.2f), Quaternion.LookRotation(Vector3.left)), new Vector3(-21.5f, 2.6f, 0f));
                yield return new WaitForSeconds(1f);
                yield return Shot("03_hall_tunnels");
                var mid = sewer.Branches[1].Sign.transform.position;
                LookAtFrom(new Pose(new Vector3(-17.5f, 0.05f, 0.3f), Quaternion.LookRotation(Vector3.left)), mid);
                yield return new WaitForSeconds(1f);
                yield return Shot("04_sign");

                // The whole stash full of turned and mirrored pieces: the cart shows every cell taken.
                var rng = new System.Random(5);
                for (int tries = 0; tries < 400; tries++)
                {
                    byte shape = (byte)rng.Next(ItemCatalog.Shapes.Length), rot = (byte)rng.Next(8);
                    byte x = (byte)rng.Next(TeamStash.Width), y = (byte)rng.Next(TeamStash.Height);
                    if (gm.Stash.CanPlace(shape, rot, x, y)) gm.Stash.Add(shape, (byte)rng.Next(5), x, y, rot);
                }
                for (byte y = 0; y < TeamStash.Height; y++)
                    for (byte x = 0; x < TeamStash.Width; x++)
                        if (gm.Stash.CanPlace(0, 0, x, y)) gm.Stash.Add(0, 4, x, y, 0);
                int cells = 0;
                foreach (var e in gm.Stash.Items) cells += ItemCatalog.Shapes[e.Shape].Cells.Length;
                Log($"cart: {gm.Stash.Items.Count} items, {cells} of {TeamStash.Width * TeamStash.Height} cells");
                yield return new WaitForSeconds(0.5f);
                var above = cart + new Vector3(0.9f, 0.05f, 1.3f);
                LookAtFrom(new Pose(above, Quaternion.LookRotation(Flat(cart - above))), cart + Vector3.up * 0.6f);
                Me.Motor.SetPitch(50f);
                yield return new WaitForSeconds(0.4f);
                yield return Shot("04b_cart_full");

                // Where the turned side tunnels meet the hall: no gaps around the openings, nothing sticking into the hall.
                for (int b = 0; b < 3; b += 2)
                {
                    var mouth = sewer.Branches[b].Mouth;
                    var inside = mouth.position + mouth.right * 2.5f + Vector3.up * 0.05f;
                    LookAtFrom(new Pose(inside, Quaternion.LookRotation(-mouth.right)), mouth.position + Vector3.up * 1.2f - mouth.right * 2f);
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot($"04c_tunnel{b}_looking_back");
                    var hall = mouth.position - mouth.right * 3f + mouth.forward * 2f + Vector3.up * 0.05f;
                    LookAtFrom(new Pose(hall, Quaternion.LookRotation(Flat(mouth.position - hall))), mouth.position + Vector3.up * 1.2f);
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot($"04d_tunnel{b}_from_hall");
                }
                // Down in the channel and the cistern: their walls go down to the bed, nothing to see under the floor.
                LookAtFrom(new Pose(new Vector3(-6f, -0.55f, 0.3f), Quaternion.LookRotation(Vector3.forward)), new Vector3(-7f, -0.4f, 1.6f));
                yield return new WaitForSeconds(0.3f);
                yield return Shot("04e_channel_side");
                LookAtFrom(new Pose(new Vector3(-16.6f, -0.65f, 0f), Quaternion.LookRotation(Vector3.right)), new Vector3(-14.8f, -0.4f, 0f));
                yield return new WaitForSeconds(0.3f);
                yield return Shot("04f_cistern_wall");
                // Sliding along the tunnel wall past the ribs and along the walkway past the cart: nothing to catch on.
                yield return Slide(new Vector3(-4.2f, 0.05f, 2.9f), 290f, 1.5f, "wall_slide"); // along -X past a rib, pressing into the +Z wall (clear of the crates)
                yield return Slide(new Vector3(-2.2f, 0.05f, -1.45f), 270f, 3f, "walkway_past_cart");
                Me.TeleportOwner(sewer.SpawnFor(slot), sewer.SpawnRotation(slot));
            }
            else yield return new WaitForSeconds(4f);

            // Everyone walks into the tunnel of the first way: the house starts after the countdown.
            yield return IntoTunnel(gm, 0, slot);
            if (Me.IsServer) yield return Shot("05_in_tunnel");
            float tw = Time.time;
            while (gm.Phase == RunPhase.Sewer && Time.time - tw < 15f) yield return null;
            yield return new WaitForSeconds(2f);
            Log($"house: phase {gm.Phase}, house {gm.Level.Index} lane {gm.Level.Lane}, layout {ActiveLevel.Layout != null}");
            if (gm.Phase != RunPhase.Playing || ActiveLevel.Layout == null) { Log("FAIL: the house did not start"); yield break; }

            if (Me.IsServer)
            {
                // The safe dial.
                var safe = SafeBox.All.FirstOrDefault(s => !s.Opened);
                if (safe != null)
                {
                    LookAtFrom(ViewPoint(safe.transform.position, 1.3f), safe.transform.position + Vector3.up * 0.5f);
                    yield return new WaitForSeconds(0.3f);
                    Me.StartCrackForTest(safe);
                    var dial = Me.Cracking;
                    dial.SetDial(dial.CodeAt(0));
                    Me.TryDial();
                    dial.SetDial(dial.CodeAt(1) + 2);
                    yield return new WaitForSeconds(0.25f);
                    yield return Shot("06_safe_dial");
                    Me.TryDial();
                    yield return new WaitForSeconds(0.08f);
                    yield return Shot("07_safe_wrong");
                    Me.StartCrackForTest(null);
                }
                // A marked robot behind a wall: one shape with one outline.
                var robot = Detector.All.OfType<RobotController>().FirstOrDefault(r => LevelLayout.FloorOf(r.transform.position.y) == 0);
                if (robot != null)
                {
                    robot.MarkServer(Session.ServerTimeF + 30f);
                    var layout = ActiveLevel.Layout;
                    var cell = new Vector2Int(Mathf.FloorToInt(robot.transform.position.x / LevelMetrics.Cell), Mathf.FloorToInt(robot.transform.position.z / LevelMetrics.Cell));
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var n = cell + d;
                        if (n.x < 0 || n.y < 0 || n.x >= layout.Width || n.y >= layout.Height) continue;
                        var spot = layout.CellCenter(0, n.x, n.y) + new Vector3(d.x, 0, d.y) * 2f;
                        if (!Physics.Linecast(spot + Vector3.up * 1.6f, robot.Position, Perception.SightMask, QueryTriggerInteraction.Ignore)) continue;
                        LookAtFrom(new Pose(spot + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(robot.Position - spot))), robot.Position);
                        yield return new WaitForSeconds(0.5f);
                        yield return Shot("08_marked_through_wall");
                        break;
                    }
                }
                // A camera: only its head turns.
                var cam = Detector.All.OfType<SecurityCamera>().FirstOrDefault(c => LevelLayout.FloorOf(c.transform.position.y) == 0);
                if (cam != null)
                {
                    LookAtFrom(ViewPoint(cam.transform.position, 2.2f), cam.transform.position);
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("09_camera_a");
                    yield return new WaitForSeconds(2.5f);
                    yield return Shot("09_camera_b");
                }
            }

            // Everyone hides in the hatch: peeking out at floor level.
            var hatch = ActiveLevel.Layout.HatchPosition;
            Me.TeleportOwner(hatch + new Vector3(1.0f + slot * 0.4f, 0.05f, 0.5f), Quaternion.LookRotation(Vector3.forward));
            yield return new WaitForSeconds(0.4f);
            Me.SetHatch(true);
            yield return new WaitForSeconds(1.2f);
            Log($"in the hatch: {Me.InHatch}, eyes {Me.Camera.transform.position.y - hatch.y:0.00} m above the floor");
            yield return Shot("10_peeking");
            float th = Time.time;
            while (gm.Phase == RunPhase.Playing && Time.time - th < 20f) yield return null;
            yield return new WaitForSeconds(2f);
            Log($"back in the sewer: phase {gm.Phase}, quota {gm.Collected}/{gm.Quota}, ways {string.Join(",", gm.RouteOptions)}");
            if (Me.IsServer) yield return Shot("11_sewer_again");

            // On without the quota: the water rises, the run is over.
            yield return IntoTunnel(gm, gm.RouteOptions.Count - 1, slot);
            float tf = Time.time;
            while (gm.Phase == RunPhase.Sewer && Time.time - tf < 15f) yield return null;
            yield return new WaitForSeconds(4.5f);
            Log($"without the quota: phase {gm.Phase}");
            if (gm.Phase != RunPhase.Failed) Log("FAIL: the run did not end");
            if (Me.IsServer) yield return Shot("12_flooded");
            yield return new WaitForSeconds(2f);
        }

        /// <summary>Walk forward for a while from a spot (yaw in degrees) and log how far the player got.</summary>
        IEnumerator Slide(Vector3 from, float yaw, float seconds, string label)
        {
            Me.TeleportOwner(from, Quaternion.Euler(0f, yaw, 0f));
            yield return new WaitForSeconds(0.2f);
            var start = Me.transform.position;
            DevFlags.Autopilot = new PlayerInputState { Move = Vector2.up };
            yield return new WaitForSeconds(seconds);
            DevFlags.Autopilot = null;
            var moved = Me.transform.position - start;
            moved.y = 0;
            Log($"{label}: moved {moved.magnitude:0.0} m in {seconds:0} s ({moved.magnitude / seconds:0.0} m/s, walk speed {Configs.Tuning.Player.Movement.WalkSpeed:0.0})");
        }

        /// <summary>Walk into the tunnel of a way (index in the options), a little apart from teammates.</summary>
        IEnumerator IntoTunnel(GameManager gm, int way, int slot)
        {
            var sewer = SewerLocation.Active;
            int tunnel = SewerBranches.Slots(gm.RouteOptions.Count)[way];
            var mouth = sewer.Branches[tunnel].Mouth;
            var spot = mouth.position + mouth.right * (2.5f + slot * 0.9f) + Vector3.up * 0.05f;
            Me.TeleportOwner(spot, Quaternion.LookRotation(-mouth.right));
            Me.Motor.SetPitch(-8f);
            yield return new WaitForSeconds(0.8f);
            Log($"in tunnel {tunnel} (way {way}): votes {gm.Votes.All.Count}, going at {gm.GoAt:0.0}");
        }
    }
}
