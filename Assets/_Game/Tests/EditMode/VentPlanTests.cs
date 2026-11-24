using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The crawl ducts in the ceiling voids (VentPlan): a hidden way from the lobby to the exit room on every house, most
    /// rooms reached, the tunnels only where the void is (no stair hole, atrium, vault or storage room), a player fits and a
    /// robot does not.
    /// </summary>
    public class VentPlanTests
    {
        static IEnumerable<LevelLayout> Houses()
        {
            for (int k = 0; k < 16; k++) yield return LevelGenerator.Generate(7001 + k * 131, k % 5, null, null, 1 + k % 3);
        }

        [Test]
        public void APlayerFitsInADuctAndARobotDoesNot()
        {
            Assert.Greater(LevelMetrics.VoidHeight, Player.PlayerController.CrouchHeight + 0.03f, "a crouched player fits");
            Assert.Less(LevelMetrics.VoidHeight, 1.6f, "no robot fits (its navmesh agent is 2 m tall)");
            Assert.LessOrEqual(LevelMetrics.FloorHeight / (2 * LevelMetrics.StepsPerFlight), 0.3f, "a stair step is walked up, with room under the player's 0.35 m");
            Assert.Greater(LevelMetrics.LandingEnd - 0.12f, 0.9f, "the landing is wide enough to turn on");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void TheDuctsLeadFromTheLobbyToTheExitRoom()
        {
            foreach (var l in Houses())
            {
                Assert.AreEqual(l.Floors, l.Vents.Count);
                var net = l.Vents[0];
                int exit = l.Plan.RoomAt(l.ExitHatchPosition + Vector3.up * 0.3f).Id;
                // the lobby, or (an atrium lobby has no ceiling of its own) the room with a grate nearest it
                var lobby = l.Lobby;
                var from = net.Grates.Where(g => g.Room == l.LobbyRoom).ToList();
                if (from.Count == 0 && lobby.Levels > 1)
                    from = net.Grates.OrderBy(g => (l.Plan.Rooms[g.Room].Center - lobby.Center).sqrMagnitude).Take(1).ToList();
                var to = net.Grates.Where(g => g.Room == exit).ToList();
                Assert.AreEqual(1, from.Count, $"seed {l.Seed}: no grate in the lobby");
                Assert.AreEqual(1, to.Count, $"seed {l.Seed}: no grate in the exit room");
                Assert.IsNotNull(VentPlan.Path(net, from[0].Cell, to[0].Cell), $"seed {l.Seed}: no duct from the lobby to the exit room");
            }
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void MostRoomsHaveAGrateAndEveryGrateIsOnTheNetwork()
        {
            int rooms = 0, grated = 0;
            foreach (var l in Houses())
                foreach (var net in l.Vents)
                {
                    rooms += l.Plan.Rooms.Count(r => r.Floor == net.Floor && r.Levels == 1 && r.Area >= 12f && r.Type != RoomType.Corridor
                        && r.Id != l.VaultRoom && r.Id != l.StorageRoom);
                    grated += net.Grates.Count(g => l.Plan.Rooms[g.Room].Type != RoomType.Corridor && l.Plan.Rooms[g.Room].Area >= 12f);
                    if (net.Grates.Count == 0) continue;
                    var first = net.Grates[0].Cell;
                    foreach (var g in net.Grates)
                    {
                        Assert.IsNotNull(VentPlan.Path(net, first, g.Cell), $"seed {l.Seed} floor {net.Floor}: a grate off the network");
                        Assert.AreEqual(g.Room, l.Plan.RoomAt(new Vector3(g.Position.x, LevelLayout.FloorY(net.Floor) + 0.3f, g.Position.z))?.Id, "the grate over its room");
                    }
                }
            Assert.Greater(grated, rooms * 0.6f, $"{grated} of {rooms} rooms have a grate");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void TheDuctsRunOnlyWhereThereIsAVoid()
        {
            foreach (var l in Houses())
                foreach (var net in l.Vents)
                    foreach (var c in net.Cells)
                    {
                        Assert.IsTrue(VentPlan.Allowed(l, net.Floor, c), $"seed {l.Seed} floor {net.Floor}: a duct at {c}");
                        foreach (var s in l.Stairs)
                            if (s.Floor == net.Floor)
                                Assert.IsFalse(PlanGenerator.StairArea(s, 0f).Overlaps(new Rect(c.x * VentPlan.Res, c.y * VentPlan.Res, VentPlan.Res, VentPlan.Res)), $"seed {l.Seed}: a duct through a stair hole");
                    }
        }

        [Test]
        public void TheCeilingFinishGoesRoundTheGrates()
        {
            var pieces = ArchitecturePlanner.CutHoles(Vector2.zero, new Vector2(4f, 4f), new List<Rect> { new Rect(1f, 1f, 1f, 1f) });
            float area = pieces.Sum(p => (p.max.x - p.min.x) * (p.max.y - p.min.y));
            Assert.AreEqual(15f, area, 1e-4f);
            foreach (var (a, b) in pieces) Assert.IsFalse(new Rect(a, b - a).Overlaps(new Rect(1.001f, 1.001f, 0.998f, 0.998f)), "a piece over the hole");
            Assert.AreEqual(1, ArchitecturePlanner.CutHoles(Vector2.zero, Vector2.one, new List<Rect> { new Rect(5f, 5f, 1f, 1f) }).Count, "a hole elsewhere leaves it whole");
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void NoGrateCutsIntoACeilingLamp()
        {
            foreach (var l in Houses())
                foreach (var net in l.Vents)
                    foreach (var g in net.Grates)
                    {
                        var room = l.Plan.Rooms[g.Room];
                        Assert.IsFalse(VentPlan.ClashesLamp(g.Cell, LevelBuilder.LightSpots(l, room)), $"seed {l.Seed}: the grate in {room.Type} on floor {net.Floor} touches a lamp");
                    }
        }

        [Test]
        public void NothingHidesAGrate()
        {
            // a ceiling of its own goes round the grates (as the style's does), so does the corridors' duct
            var ceiling = new Material(Shader.Find("Hidden/InternalErrorShader"));
            var g = new StyleGeometry
            {
                RoomLooks = new[] { RoomType.Office, RoomType.Corridor, RoomType.Meeting, RoomType.Restroom, RoomType.Kitchen }
                    .Select(t => new RoomLook { Room = t, Ceiling = ceiling, CeilingTile = 1f }).ToArray(),
            };
            try
            {
                foreach (var l in Houses().Take(6))
                {
                    var shell = new LevelShell();
                    var holes = new List<(Rect hole, float y)>();
                    foreach (var net in l.Vents)
                        foreach (var grate in net.Grates)
                        {
                            var h = new Rect(grate.Cell.x * VentPlan.Res, grate.Cell.y * VentPlan.Res, VentPlan.Res, VentPlan.Res);
                            shell.CeilingHoles.Add(new CeilingHole { Floor = net.Floor, Area = h });
                            holes.Add((new Rect(h.x + 0.02f, h.y + 0.02f, h.width - 0.04f, h.height - 0.04f), grate.Position.y));
                        }
                    foreach (var piece in ArchitecturePlanner.Plan(shell, l, g))
                    {
                        if (piece.Kind != DecorKind.CeilingFinish) continue;
                        var r = new Rect(piece.Position.x - piece.Scale.x / 2f, piece.Position.z - piece.Scale.y / 2f, piece.Scale.x, piece.Scale.y);
                        foreach (var (h, y) in holes)
                            Assert.IsFalse(Mathf.Abs(piece.Position.y - y) < 0.1f && r.Overlaps(h), $"seed {l.Seed}: a ceiling over the grate at {h.center}");
                    }
                    foreach (var part in UtilityPlan.Make(l, shell))
                    {
                        if (part.Slot != UtilitySlot.Duct) continue;
                        var r = new Rect(part.Center.x - part.Size.x / 2f, part.Center.z - part.Size.z / 2f, part.Size.x, part.Size.z);
                        foreach (var (h, y) in holes)
                            Assert.IsFalse(Mathf.Abs(part.Center.y - y) < 0.5f && r.Overlaps(h), $"seed {l.Seed}: a duct under the grate at {h.center}");
                    }
                }
            }
            finally { Object.DestroyImmediate(ceiling); }
        }

        [Category("Slow")] // generates many houses (Tools/dev.sh: SLOW=1 runs it)
        [Test]
        public void SomeGratesHaveAWayUpButNeverTheFirstRoom()
        {
            int climbs = 0, grates = 0;
            var kinds = new System.Collections.Generic.HashSet<PropKind>();
            foreach (var l in Houses())
            {
                Assert.AreEqual(-1, l.Vents[0].Grates[0].ClimbDir, $"seed {l.Seed}: a way up in the team's first room");
                foreach (var net in l.Vents)
                    foreach (var g in net.Grates)
                    {
                        // (a hospital's patient and treatment rooms give their walls to the beds and machines first: Furnisher.Climb)
                        if (g.Room >= 0 && l.Plan.Rooms[g.Room].Type is RoomType.Ward or RoomType.Operating or RoomType.Imaging or RoomType.NurseStation) continue;
                        grates++;
                        if (g.ClimbDir < 0) continue;
                        climbs++;
                        Assert.AreNotEqual(l.LobbyRoom, g.Room, "never in the lobby");
                        // the room's own furniture: the top right under the grate, the step beside it
                        var under = new Vector3(g.Position.x, LevelLayout.FloorY(net.Floor), g.Position.z);
                        var type = l.Plan.Rooms[g.Room].Type;
                        Assert.IsTrue(Furnisher.HasClimb(l, type, under, VentPlan.ClimbSide(g.ClimbDir)), $"seed {l.Seed}: half a way up in a {type}");
                        kinds.Add(Furnisher.ClimbOf(type).Top);
                    }
            }
            // (corridors, halls, the lobby, the exit and the robots' rooms never have one; a busy room may have no place for it)
            // (the whole-room walk check keeps a busy room's way up out more often than it used to; grates clear of the lamps and
            // the smaller hospitals leave fewer rooms with a place for one: a thirtieth at least)
            Assert.That(climbs, Is.InRange(grates / 30, grates * 3 / 4), $"{climbs} of {grates} grates have a way up");
            Assert.GreaterOrEqual(kinds.Count, 3, "not the same pile everywhere: " + string.Join(", ", kinds));
            Assert.IsFalse(kinds.Contains(PropKind.CrateStack), "crates only where crates are kept");
        }

        /// <summary>The highest solid top of a piece at a point of its own floor plan (0: nothing there).</summary>
        static float TopAt(GameObject go, float x, float z)
        {
            float top = 0f;
            foreach (var c in go.GetComponentsInChildren<Collider>())
                if (c.Raycast(new Ray(go.transform.position + new Vector3(x, 4f, z), Vector3.down), out var hit, 5f)) top = Mathf.Max(top, hit.point.y - go.transform.position.y);
            return top;
        }

        [Test]
        public void EveryRoomsWayUpCanBeClimbed()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            const float jump = 1.05f;
            foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
            {
                var set = Furnisher.ClimbOf(type);
                float Height(PropKind kind, float at)
                {
                    var prefab = art.Prop(kind);
                    Assert.IsNotNull(prefab, $"{type}: no {kind}");
                    var go = Object.Instantiate(prefab, new Vector3(700f, 0f, 700f), Quaternion.identity);
                    try
                    {
                        Physics.SyncTransforms();
                        // most of what is under the feet (a bench's slats have gaps between them)
                        var tops = new System.Collections.Generic.List<float>();
                        for (float x = -0.2f; x <= 0.21f; x += 0.08f)
                            for (float z = -0.14f; z <= 0.15f; z += 0.07f) tops.Add(TopAt(go, at + x, z));
                        tops.Sort();
                        return tops[tops.Count / 2];
                    }
                    finally { Object.DestroyImmediate(go); }
                }
                // (a desk's middle holds its screen: beside it)
                float step = Height(set.Step, set.Step is PropKind.Desk or PropKind.DoctorDesk or PropKind.WorkTable or PropKind.ReadingTable ? 0.55f : 0f);
                if (set.Top == PropKind.CrateStack) continue; // as it always was
                float top = Height(set.Top, 0.1f);
                Assert.That(step, Is.InRange(0.4f, 1.0f), $"{type}: from the floor onto the {set.Step}");
                Assert.LessOrEqual(top - step, jump - 0.05f, $"{type}: from the {set.Step} onto the {set.Top}");
                Assert.IsTrue(VentGrate.IntoDuct(new Vector3(0f, top + jump, 0f), new Vector3(0f, LevelMetrics.WallHeight, 0f), true, true), $"{type}: from the {set.Top} ({top:0.00} m) into the duct");
                Assert.Less(top + 1.1f, LevelMetrics.WallHeight, $"{type}: crouched on the {set.Top}, under the ceiling");
                // the pieces stand side by side: touching, not in one another, the top under the grate
                foreach (bool flip in new[] { false, true })
                {
                    var pieces = Furnisher.ClimbPieces(type, Vector3.zero, Vector3.right, flip);
                    for (int i = 0; i < pieces.Count; i++)
                        for (int j = 0; j < i; j++)
                        {
                            var a = Furnisher.FootAt(pieces[i].kind, pieces[i].pos, pieces[i].yaw);
                            var b = Furnisher.FootAt(pieces[j].kind, pieces[j].pos, pieces[j].yaw);
                            a = new Rect(a.x + 0.005f, a.y + 0.005f, a.width - 0.01f, a.height - 0.01f);
                            Assert.IsFalse(a.Overlaps(b), $"{type}: {pieces[i].kind} in {pieces[j].kind}");
                        }
                    Assert.Less(Mathf.Abs(pieces[pieces.Count - 1].pos.x), 1.6f, $"{type}: the step within reach");
                }
            }
        }

        [Test]
        public void ACrouchedJumpFromTheCratesGetsIntoTheDuct()
        {
            var grate = new Vector3(0f, LevelMetrics.WallHeight, 0f);
            float crates = 1.3f, jump = 1.05f; // (the lowest top: a filing cabinet)
            Assert.IsTrue(VentGrate.IntoDuct(new Vector3(0.1f, crates + jump, 0f), grate, true, true), "the top of the jump from the crates");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0.1f, crates + jump, 0f), grate, false, true), "standing: the head bumps");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0.1f, 0.92f + jump, 0f), grate, true, true), "from the step: too low");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0.8f, crates + jump, 0f), grate, true, true), "beside the hole");
            Assert.IsFalse(VentGrate.IntoDuct(new Vector3(0f, crates + jump, 0f), grate, true, false), "falling back down");
        }

        [Test]
        public void TheStorageRobotWaddlesAndTiltsAsItClimbs()
        {
            var still = Stealth.Game.StorageBotAnimator.Pose(0f, 0f, 1f);
            Assert.AreEqual(0f, still.roll, 1e-4f);
            Assert.AreEqual(0f, still.bob, 1e-4f);
            var walk = Stealth.Game.StorageBotAnimator.Pose(2.5f, 0f, 1f);
            Assert.Greater(Mathf.Abs(walk.roll), 1f, "it sways walking");
            Assert.Less(Stealth.Game.StorageBotAnimator.Pose(0f, 1.8f, 0f).pitch, -30f, "nose up climbing up");
            Assert.Greater(Stealth.Game.StorageBotAnimator.Pose(0f, -1.8f, 0f).pitch, 30f, "nose down climbing down");
        }
    }
}
