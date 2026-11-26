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
        /// The special places of a house (<c>SEED=7 LEVEL=3 Tools/dev.sh run features</c>): the location's point of
        /// interest, a crate searched, the key door and its key, the hidden room seen from outside and from inside,
        /// the stun cube, the legs of a walking player.
        /// </summary>
        IEnumerator FeaturesScenario()
        {
            DevFlags.Peaceful = true;
            var gm = GameManager.Instance;
            var l = ActiveLevel.Layout;
            var inv = Me.GetComponent<PlayerInventory>();
            yield return new WaitForSeconds(1f);
            Me.SetHatch(false);
            yield return new WaitForSeconds(0.5f);
            Log($"house: {HouseKinds.Title(l.Seed, l.Kind)} ({l.Location}), POI {(l.PointOfInterest >= 0 ? l.Props[l.PointOfInterest].Kind.ToString() : "none")}, crates {l.Crates.Count}, key doors {l.KeyDoors.Count}, hidden room {l.HiddenRoom}");

            Vector3 Front(PropPlacement p, float d) => p.Position + Quaternion.Euler(0f, p.RotationY, 0f) * Vector3.forward * d + Vector3.up * 0.05f;
            if (l.PointOfInterest >= 0)
            {
                var p = l.Props[l.PointOfInterest];
                LookAtFrom(new Pose(Front(p, 2.6f), Quaternion.identity), p.Position + Vector3.up * 0.9f);
                yield return new WaitForSeconds(0.5f);
                Log($"poi: use {Me.UseTarget?.Kind}, prompt «{Me.UsePrompt}»");
                yield return Shot("01_point_of_interest");
                if (p.Kind == PropKind.MedTable)
                {
                    Me.TeleportOwner(Front(p, 1.2f), Quaternion.identity);
                    yield return new WaitForSeconds(0.3f);
                    gm.UseServer(Me, UseKind.MedTable, l.PointOfInterest);
                    Log($"med table: hp {Me.Hp}");
                }
                if (p.Kind == PropKind.Workbench)
                {
                    // the car upgrade bench: the engine's first level for its recipe
                    foreach (byte shape in gm.UpgradeBook.Recipe(Vehicles.CarUpgrade.Engine, 1)) inv.GiveServer(shape, 0);
                    Me.TeleportOwner(Front(p, 1.2f), Quaternion.identity);
                    yield return new WaitForSeconds(0.3f);
                    gm.CarUpgradeServer(Me, Vehicles.CarUpgrade.Engine);
                    Log($"upgrade bench: engine level {gm.Upgrades.Level(Vehicles.CarUpgrade.Engine)}, backpack {string.Join(",", inv.Items.Select(e => ItemCatalog.Shapes[e.Shape].Id))}");
                }
                if (p.Kind == PropKind.Programmer)
                    Log($"programmer: stun cubes lying {WorldItem.All.Count(w => w.Shape == ItemCatalog.Stun)}");
            }

            if (l.Crates.Count > 0)
            {
                int c = l.Crates[0];
                var p = l.Props[c];
                Me.TeleportOwner(Front(p, 1.0f), Quaternion.LookRotation(-(Quaternion.Euler(0f, p.RotationY, 0f) * Vector3.forward)));
                yield return new WaitForSeconds(0.3f);
                int before = WorldItem.All.Count;
                gm.UseServer(Me, UseKind.Crate, c);
                yield return new WaitForSeconds(0.8f);
                LookAtFrom(new Pose(Front(p, 2.0f), Quaternion.identity), p.Position + Vector3.up * 0.4f);
                yield return new WaitForSeconds(0.3f);
                Log($"crate: items {before} -> {WorldItem.All.Count}, searched {World.IsOpened(OpenKeys.Crate(c))}");
                yield return Shot("02_crate_searched");
            }

            foreach (var k in l.KeyDoors.Take(1))
            {
                var pose = l.AutoDoors[k.Door];
                var door = AutoDoor.All.FirstOrDefault(d => d.Index == k.Door);
                LookAtFrom(new Pose(pose.position + pose.rotation * Vector3.forward * 2.2f + Vector3.up * 0.05f, Quaternion.identity), pose.position + Vector3.up * 1.2f);
                yield return new WaitForSeconds(0.5f);
                Log($"key door: locked {door?.Locked}, needs key {door?.NeedsKey}, prompt «{Me.UsePrompt}»");
                yield return Shot("03_key_door");
                var key = WorldItem.All.FirstOrDefault(w => w.Shape == ItemCatalog.Key);
                if (key != null)
                {
                    LookAtFrom(new Pose(key.transform.position + new Vector3(1.2f, 0.1f, 1.2f) - Vector3.up * key.transform.position.y + Vector3.up * LevelLayout.FloorY(LevelLayout.FloorOf(key.transform.position.y)), Quaternion.identity), key.transform.position);
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot("04_key");
                }
                inv.GiveServer(ItemCatalog.Key, 4);
                Me.TeleportOwner(pose.position + pose.rotation * Vector3.forward * 1.2f + Vector3.up * 0.05f, Quaternion.LookRotation(-(pose.rotation * Vector3.forward)));
                yield return new WaitForSeconds(0.4f);
                gm.UseServer(Me, UseKind.KeyDoor, k.Door);
                yield return new WaitForSeconds(0.5f);
                Log($"key door: opened {!door?.Locked}, keys left {inv.Items.Count(e => e.Shape == ItemCatalog.Key)}");
            }

            if (l.HiddenRoom >= 0)
            {
                var plan = l.Plan;
                var o = plan.Openings.First(x => x.Kind == EdgeType.Illusion);
                var w = plan.Walls[o.Wall];
                var mid = w.At(o.At);
                var n = new Vector2(-w.Dir.y, w.Dir.x);
                var into = plan.RoomAt(new Vector3(mid.x + n.x, 0.3f + LevelLayout.FloorY(w.Floor), mid.y + n.y))?.Id == l.HiddenRoom ? n : -n;
                float y = LevelLayout.FloorY(w.Floor) + 0.05f;
                var outside = new Vector3(mid.x - into.x * 2.5f, y, mid.y - into.y * 2.5f);
                LookAtFrom(new Pose(outside, Quaternion.identity), new Vector3(mid.x, y + 1.3f, mid.y));
                yield return new WaitForSeconds(0.4f);
                yield return Shot("05_illusion_wall_outside");
                // walk through it
                Me.TeleportOwner(new Vector3(mid.x - into.x * 1.0f, y, mid.y - into.y * 1.0f), Quaternion.LookRotation(new Vector3(into.x, 0f, into.y)));
                DevFlags.Autopilot = new PlayerInputState { Move = Vector2.up };
                yield return new WaitForSeconds(1.2f);
                DevFlags.Autopilot = null;
                var room = plan.RoomAt(Me.transform.position + Vector3.up * 0.3f);
                Log($"hidden room: walked through the illusion into room {room?.Id} (hidden {l.HiddenRoom}), map sees through it {plan.SightShare(w.Floor, mid - into * 1.5f, mid + into * 1.5f) > 0.99f}");
                yield return Shot("06_inside_hidden_room");
            }

            inv.GiveServer(ItemCatalog.Stun, 2);
            gm.GadgetServer(Me);
            yield return new WaitForSeconds(0.5f);
            Log($"stun: {Detector.All.Count(d => d.IsHacked)} of {Detector.All.Count} robots and cameras out, stun cubes left {inv.Items.Count(e => e.Shape == ItemCatalog.Stun)}");

            // the legs of a walking player (seen from the side)
            Me.TeleportOwner(l.HatchPosition + new Vector3(2.5f, 0.05f, 0f), Quaternion.LookRotation(Vector3.forward));
            DevFlags.Autopilot = new PlayerInputState { Move = Vector2.up };
            yield return new WaitForSeconds(0.6f);
            DevFlags.Autopilot = null;
            Log("DONE");
        }
    }
}
