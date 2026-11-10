using System.Collections;
using System.Linq;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.UI;
using UnityEngine;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// Between houses (<c>LEVEL=3 Tools/dev.sh run sewer 1</c>): hack a locked door, crack a safe, hide in the hatch
        /// (everyone), go down into the sewer, pay the quota, vote, go on to the next house; then leave again without the
        /// quota and see the run end. A client does the hiding and voting too.
        /// </summary>
        IEnumerator SewerScenario()
        {
            DevFlags.FreezeRobots = true;
            DevFlags.Peaceful = true;
            var gm = GameManager.Instance;
            float t0 = Time.time;
            bool expectClients = Me.IsServer && Arg("-autotest-linger") != null;
            while (expectClients && PlayerController.All.Count < 2 && Time.time - t0 < 25f) yield return null;
            yield return new WaitForSeconds(1f);
            var layout = ActiveLevel.Layout;
            Log($"house {gm.Level.Index} lane {gm.Level.Lane}: locked doors {layout.LockedDoors.Count}, safes {SafeBox.All.Count}, players {PlayerController.All.Count}");

            if (Me.IsServer)
            {
                // A locked door: hold E next to it.
                var door = AutoDoor.All.FirstOrDefault(d => d.Locked && LevelLayout.FloorOf(d.transform.position.y) == 0) ?? AutoDoor.All.FirstOrDefault(d => d.Locked);
                if (door != null)
                {
                    var side = door.transform.forward * 1.2f;
                    LookAtFrom(new Pose(door.transform.position + side + Vector3.up * 0.05f, Quaternion.LookRotation(-side)), door.transform.position + Vector3.up * 2.2f);
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot("1_locked_door");
                    DevFlags.AutoInteract = true;
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot("2_hacking_door");
                    yield return new WaitForSeconds(2.3f);
                    DevFlags.AutoInteract = false;
                    yield return new WaitForSeconds(0.5f);
                    Log($"door {door.Index}: locked={door.Locked} (should be false)");
                    if (door.Locked) Log("FAIL: the door is still locked");
                    yield return Shot("3_door_unlocked");
                }
                else Log("no locked door in this house");

                // A safe: the dial minigame, then the loot.
                var safe = SafeBox.All.FirstOrDefault(s => !s.Opened);
                if (safe != null)
                {
                    var view = ViewPoint(safe.transform.position, 1.3f);
                    LookAtFrom(view, safe.transform.position + Vector3.up * 0.5f);
                    yield return new WaitForSeconds(0.3f);
                    Me.StartCrackForTest(safe);
                    var dial = Me.Cracking;
                    dial.SetDial(dial.CodeAt(0) + 3);
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot("4_safe_dial_near");
                    Me.TryDial(); // wrong: clank
                    yield return new WaitForSeconds(0.1f);
                    yield return Shot("5_safe_wrong");
                    int items = WorldItem.All.Count;
                    for (int i = 0; i < dial.Digits; i++)
                    {
                        dial.SetDial(dial.CodeAt(i));
                        yield return new WaitForSeconds(0.3f);
                        if (i == 1) yield return Shot("6_safe_digit");
                        Me.TryDial();
                    }
                    yield return new WaitForSeconds(0.6f);
                    Log($"safe {safe.Index}: opened={safe.Opened}, items {items} -> {WorldItem.All.Count}");
                    if (!safe.Opened) Log("FAIL: the safe did not open");
                    yield return Shot("7_safe_open");
                }
                else Log("no safe in this house");
            }

            // Everyone hides in the hatch.
            yield return HideInHatch(gm, "8");
            if (gm.Phase != RunPhase.Sewer) { Log($"FAIL: not in the sewer (phase {gm.Phase})"); yield break; }
            yield return new WaitForSeconds(1f);
            Log($"in the sewer: quota {gm.Collected}/{gm.Quota}, options {string.Join(",", gm.RouteOptions)}, route open {RouteScreen.IsOpen}");
            yield return Shot("10_route_map");
            RouteScreen.CloseForTest();
            Me.TeleportOwner(SewerLocation.Active.SpawnFor(PlayerLooks.SlotOf(Me.ClientId)) + new Vector3(-1.2f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            Me.Motor.SetPitch(5f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("10b_sewer_tunnel");
            Me.TeleportOwner(SewerLocation.Active.TradeSpot.position + new Vector3(-2.5f, 0.05f, PlayerLooks.SlotOf(Me.ClientId) * 0.8f), Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(0.6f);
            yield return Shot("10c_sewer_board");

            if (Me.IsServer) gm.AddCollectedServer(Mathf.Max(0, gm.Quota - gm.Collected)); // the team sells enough
            yield return new WaitForSeconds(0.8f);
            int before = gm.Level.Index;
            gm.RequestVote(gm.RouteOptions[gm.RouteOptions.Count - 1]);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("11_voted");
            float tv = Time.time;
            while (gm.Level.Index == before && Time.time - tv < 15f) yield return null;
            yield return new WaitForSeconds(1.5f);
            Log($"next house: {before} -> {gm.Level.Index} lane {gm.Level.Lane}, phase {gm.Phase}");
            if (gm.Level.Index != before + 1 || gm.Phase != RunPhase.Playing) Log("FAIL: did not go on to the next house");
            yield return Shot("12_next_house");

            // Leave again without the quota: the run ends in the sewer.
            yield return HideInHatch(gm, "13");
            yield return new WaitForSeconds(1f);
            gm.RequestVote(gm.RouteOptions[0]);
            float tf = Time.time;
            while (gm.Phase == RunPhase.Sewer && Time.time - tf < 15f) yield return null;
            yield return new WaitForSeconds(4f);
            Log($"without the quota: phase {gm.Phase}");
            if (gm.Phase != RunPhase.Failed) Log("FAIL: the run did not end");
            yield return Shot("14_flooded");
            yield return new WaitForSeconds(3f);
        }

        IEnumerator HideInHatch(GameManager gm, string shot)
        {
            var hatch = ActiveLevel.Layout.HatchPosition;
            int slot = PlayerLooks.SlotOf(Me.ClientId);
            Me.TeleportOwner(hatch + new Vector3(1.2f + slot * 0.5f, 0.05f, 0.6f), Quaternion.LookRotation(Vector3.left));
            yield return new WaitForSeconds(0.5f);
            Me.SetHatch(true);
            yield return new WaitForSeconds(0.8f);
            Log($"in the hatch: {Me.InHatch}, team {PlayerController.All.Count(p => p.InHatch)}/{PlayerController.All.Count}, leaving at {gm.LeaveAt:0.0}");
            yield return Shot($"{shot}_in_hatch");
            float t = Time.time;
            while (gm.Phase == RunPhase.Playing && Time.time - t < 20f) yield return null;
            yield return new WaitForSeconds(1.5f);
            yield return Shot($"{shot}_sewer");
        }
    }
}
