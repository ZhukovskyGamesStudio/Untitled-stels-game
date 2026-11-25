using System.Linq;
using NUnit.Framework;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The storage mule as a raccoon with a backpack: it leaps up into a grate after a crouch and drops out of one after
    /// hanging, it is petted only standing on the floor, its backpack grows with the upgrade and carries the stash wrapped
    /// round it, its charms swing and hang down again, its body plays the take for what the server says it does — and
    /// the prefab is put together so: facing forward, on its paws, the pack on its back, every take in its animator.
    /// </summary>
    public class RaccoonTests
    {
        const string BotPrefab = "Assets/_Game/Prefabs/Vehicles/StorageBot.prefab";

        [Test]
        public void ItCrouchesThenLeapsUpAndHangsThenDrops()
        {
            Assert.AreEqual(0f, StorageBotRules.LeapLift(0f));
            Assert.AreEqual(0f, StorageBotRules.LeapLift(StorageBotRules.LeapCrouch * 0.9f), "still crouching");
            float half = StorageBotRules.LeapLift(StorageBotRules.LeapCrouch + StorageBotRules.LeapTime * 0.5f);
            Assert.Greater(half, 0.5f, "the leap is fastest at its start");
            Assert.AreEqual(1f, StorageBotRules.LeapLift(StorageBotRules.LeapCrouch + StorageBotRules.LeapTime));
            Assert.AreEqual(0f, StorageBotRules.DropLower(StorageBotRules.DropHang * 0.9f), "still hanging");
            Assert.Less(StorageBotRules.DropLower(StorageBotRules.DropHang + StorageBotRules.DropTime * 0.5f), 0.5f, "the fall quickens");
            Assert.AreEqual(1f, StorageBotRules.DropLower(StorageBotRules.DropHang + StorageBotRules.DropTime));
        }

        [Test]
        public void ItIsPettedOnlyStandingOnTheFloor()
        {
            Assert.IsTrue(StorageBotRules.CanPet(BotMode.Waiting, BotMotion.Floor));
            Assert.IsTrue(StorageBotRules.CanPet(BotMode.Coming, BotMotion.Floor));
            Assert.IsFalse(StorageBotRules.CanPet(BotMode.WaitingAbove, BotMotion.Duct), "up in a duct");
            Assert.IsFalse(StorageBotRules.CanPet(BotMode.Coming, BotMotion.Ladder), "on the ladder");
            Assert.IsFalse(StorageBotRules.CanPet(BotMode.ToGrate, BotMotion.Floor), "on its way out");
        }

        [Test]
        public void TheStashGridWrapsRoundThePacksSidesTopAndBack()
        {
            var box = new PackWrap.Box(0.3f, 0.3f, 0.2f);
            // three columns: the left side, the top, the right side
            var left = PackWrap.OnBox(0.5f, 0.5f, 3, 5, box);
            var top = PackWrap.OnBox(1.5f, 0.5f, 3, 5, box);
            var right = PackWrap.OnBox(2.5f, 0.5f, 3, 5, box);
            Assert.AreEqual(PackWrap.Face.Left, left.Face);
            Assert.AreEqual(PackWrap.Face.Top, top.Face);
            Assert.AreEqual(PackWrap.Face.Right, right.Face);
            Assert.AreEqual(0f, left.Point.x + right.Point.x, 1e-5f, "the sides mirror each other");
            Assert.AreEqual(left.Point.y, right.Point.y, 1e-5f);
            Assert.Greater(top.Point.z, 0f, "the first row at the head end");
            // the last rows go down the back, never the front or the bottom
            var back = PackWrap.OnBox(1.5f, 4.5f, 3, 5, box);
            Assert.AreEqual(PackWrap.Face.Back, back.Face);
            Assert.AreEqual(-0.15f, back.Point.z, 1e-5f);
            for (float u = 0.05f; u < 3f; u += 0.1f)
                for (float v = 0.05f; v < 5f; v += 0.1f)
                {
                    var p = PackWrap.OnBox(u, v, 3, 5, box);
                    Assert.LessOrEqual(p.Point.z, 0.15f + 1e-5f, "nothing on the front (the head)");
                    Assert.GreaterOrEqual(p.Point.y, -0.2f - 1e-5f, "nothing under the bottom (his back)");
                    Assert.Greater(Vector3.Dot(p.Normal, p.Point - new Vector3(0f, -0.1f, 0f)), 0f, "faces point out of the box (its middle half its height under the top)");
                    Assert.AreEqual(0f, Vector3.Dot(p.Across, p.Normal), 1e-5f);
                    Assert.AreEqual(0f, Vector3.Dot(p.Down, p.Normal), 1e-5f);
                }
        }

        [Test]
        public void ALongThingHangsUprightOnASideWhenItLiesAcrossTheGrid()
        {
            var box = new PackWrap.Box(0.3f, 0.3f, 0.2f);
            byte shovel = ItemCatalog.Shovel;
            // upright in the grid, on the middle column: it lies along the top (flat, its length along the body)
            var along = PackWrap.Lay(new[] { new InventoryEntry { Shape = shovel, X = 1, Y = 0, Rot = 0 } }, 3, 5, box, _ => 0.05f)[0];
            Assert.AreEqual(PackWrap.Face.Top, along.Face);
            var lengthwise = along.Rotation * Quaternion.Inverse(StashCart.Place(new InventoryEntry { Shape = shovel, Rot = 0 }).rotation) * Vector3.forward;
            Assert.AreEqual(1f, Mathf.Abs(lengthwise.z), 1e-4f, "along the body");
            // the same thing lying across a wide grid from its left edge: it hangs on the left side, upright
            var across = PackWrap.Lay(new[] { new InventoryEntry { Shape = shovel, X = 0, Y = 1, Rot = 1 } }, 6, 7, box, _ => 0.05f)[0];
            Assert.AreEqual(PackWrap.Face.Left, across.Face);
            var length = ItemCatalog.Size(ItemCatalog.RotatedCells(shovel, 1));
            // the thing's own grid-x (its length when turned) runs up the side
            var gridX = across.Rotation * Quaternion.Inverse(StashCart.Place(new InventoryEntry { Shape = shovel, Rot = 1 }).rotation) * Vector3.right;
            Assert.Greater(length.x, length.y);
            Assert.AreEqual(1f, Mathf.Abs(gridX.y), 1e-4f, "upright on the side");
        }

        [Test]
        public void ThingsThatOverlapOnAFacePileUpAndKeepTheirSize()
        {
            var box = new PackWrap.Box(0.2f, 0.2f, 0.15f);
            var items = new[] { new InventoryEntry { Shape = 0, X = 1, Y = 0 }, new InventoryEntry { Shape = 0, X = 1, Y = 1 } };
            var laid = PackWrap.Lay(items, 3, 5, box, _ => 0.06f);
            Assert.AreEqual(PackWrap.Face.Top, laid[0].Face);
            Assert.AreEqual(PackWrap.Face.Top, laid[1].Face);
            Assert.Greater(laid[1].Position.y, laid[0].Position.y + 0.05f, "the second lies on the first");
            Assert.AreEqual(Vector3.one, laid[0].Scale, "its own size");
        }

        [Test]
        public void TheRaccoonsNameIsOneKeyInEveryText()
        {
            Loc.Load(new[] { "key,ru,en\nname.x,Линус,Linus\nname.x.gen,Линуса,Linus\nt.come,{=name.x} бежит к вам,{=name.x} is running to you\nt.box,Рюкзак {=name.x.gen}: {0},{=name.x}'s backpack: {0}" }, out _);
            try
            {
                Loc.Use(Language.Ru, save: false);
                Assert.AreEqual("Линус бежит к вам", Loc.T("t.come"));
                Assert.AreEqual("Рюкзак Линуса: 7", Loc.F("t.box", 7));
                Loc.Use(Language.En, save: false);
                Assert.AreEqual("Linus's backpack: 7", Loc.F("t.box", 7));
            }
            finally
            {
                Loc.Reload();
                Loc.Use(Language.Ru, save: false);
            }
            foreach (var kv in Loc.Table)
                foreach (var text in kv.Value)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text ?? "", @"\{=([\w\.]+)\}"))
                        Assert.IsTrue(Loc.Has(m.Groups[1].Value), $"{kv.Key}: no key {m.Groups[1].Value}");
            Assert.AreEqual("Линус", Loc.T("name.raccoon"));
        }

        [Test]
        public void ItsBodyPlaysTheTakeForWhatItDoes()
        {
            Assert.AreEqual(StorageBotRules.Take.Move, StorageBotRules.TakeOf(BotMotion.Floor, false));
            Assert.AreEqual(StorageBotRules.Take.Pet, StorageBotRules.TakeOf(BotMotion.Floor, true));
            Assert.AreEqual(StorageBotRules.Take.Climb, StorageBotRules.TakeOf(BotMotion.Ladder, true), "a pat does not stop the climb");
            Assert.AreEqual(StorageBotRules.Take.VentUp, StorageBotRules.TakeOf(BotMotion.VentUp, false));
            Assert.AreEqual(StorageBotRules.Take.VentDown, StorageBotRules.TakeOf(BotMotion.VentDown, false));
            Assert.AreEqual(StorageBotRules.Take.Move, StorageBotRules.TakeOf(BotMotion.Duct, false), "crawling: the gait, pressed flat");
            Assert.AreEqual(StorageBotRules.Take.Curl, StorageBotRules.TakeOf(BotMotion.Curled, true), "asleep in the trunk");
            Assert.AreEqual(1f, StorageBotRules.Stride(1f));
            Assert.AreEqual(2f, StorageBotRules.Stride(StorageBotRules.RunTakeSpeed * 2f), 1e-5f);
            Assert.Less(StorageBotRules.ClimbRate(-StorageBotRules.LadderSpeed), 0f, "down the ladder: the climb backwards");
            Assert.AreEqual(0f, StorageBotRules.ClimbRate(0f));
        }

        [Test]
        public void ItsCharmsSwingAndHangDownAgain()
        {
            var pivot = Vector3.zero;
            var tip = new Vector3(0f, -0.1f, 0f);
            var prev = tip;
            // the pack jerks forward: the charm swings back, never past its limit, always on its string
            float most = 0f;
            for (int i = 0; i < 30; i++)
            {
                pivot += new Vector3(0f, 0f, i < 5 ? 0.03f : 0f);
                tip = DangleMath.Step(pivot, Vector3.down, 0.1f, tip, ref prev, Physics.gravity, 1f / 60f);
                Assert.AreEqual(0.1f, Vector3.Distance(pivot, tip), 1e-4f);
                most = Mathf.Max(most, Vector3.Angle(Vector3.down, tip - pivot));
            }
            Assert.Greater(most, 10f, "it swings");
            Assert.LessOrEqual(most, DangleMath.MaxSwing + 0.01f);
            for (int i = 0; i < 600; i++) tip = DangleMath.Step(pivot, Vector3.down, 0.1f, tip, ref prev, Physics.gravity, 1f / 60f);
            Assert.Less(Vector3.Angle(Vector3.down, tip - pivot), 2f, "it comes to rest hanging down");
        }

        [Test]
        public void CalledItSitsDownWhereItCameAndWaits()
        {
            Assert.IsTrue(StorageBotRules.SitsDown(true, true, false), "come to the caller: it sits");
            Assert.IsFalse(StorageBotRules.SitsDown(false, true, false), "back from hiding, set down: it stands");
            Assert.IsFalse(StorageBotRules.SitsDown(true, false, false), "waiting up in a duct");
            Assert.IsFalse(StorageBotRules.SitsDown(true, true, true), "still on its way");
            Assert.AreEqual(StorageBotRules.Take.Sit, StorageBotRules.TakeOf(BotMotion.Sit, false));
            Assert.AreEqual(StorageBotRules.Take.Pet, StorageBotRules.TakeOf(BotMotion.Sit, true), "petted sitting: the reaction, then sat again");
            Assert.IsTrue(StorageBotRules.CanPet(BotMode.Waiting, BotMotion.Sit), "a sitting raccoon can be petted");
        }

        [Test]
        public void SatItTurnsItsHeadToAPlayerButNeverRoundToTheBack()
        {
            var s = new MuleSettings { LookRange = 8f, NeckYaw = 60f, NeckGiveUp = 115f, NeckPitch = 25f };
            var head = new Vector3(0f, 0.5f, 0f);
            var ahead = StorageBotRules.HeadTurn(Vector3.forward, head, new Vector3(1f, 0.5f, 3f), s);
            Assert.AreEqual(Vector3.SignedAngle(Vector3.forward, new Vector3(1f, 0f, 3f), Vector3.up), ahead.x, 0.01f, "a player off to its right: turned right at them");
            Assert.Greater(ahead.x, 0f);
            var side = StorageBotRules.HeadTurn(Vector3.forward, head, new Vector3(-3f, 0.5f, -0.5f), s);
            Assert.AreEqual(-60f, side.x, 0.01f, "off past the neck's reach (~99° to the left): followed only as far as it goes");
            Assert.AreEqual(Vector2.zero, StorageBotRules.HeadTurn(Vector3.forward, head, new Vector3(0.3f, 0.5f, -3f), s), "right behind it: no turn at all");
            Assert.AreEqual(Vector2.zero, StorageBotRules.HeadTurn(Vector3.forward, head, new Vector3(0f, 0.5f, 12f), s), "too far off");
            var up = StorageBotRules.HeadTurn(Vector3.forward, head, new Vector3(0f, 1.7f, 0.6f), s);
            Assert.AreEqual(25f, up.y, 0.01f, "a player standing over it: it looks up only so far");
            Assert.AreEqual(0f, up.x, 0.01f);
        }

        [Test]
        public void ItsPawsStandOnTheFloorUnderIt()
        {
            Assert.AreEqual(-0.12f, StorageBotRules.Ground(1.12f, 1f), 1e-5f, "the navmesh a little over the floor: lowered onto it");
            Assert.AreEqual(0.04f, StorageBotRules.Ground(0.96f, 1f), 1e-5f, "a little under it: raised");
            Assert.AreEqual(0f, StorageBotRules.Ground(1f, null), "nothing under it");
            Assert.AreEqual(0f, StorageBotRules.Ground(1f, -7f), "over the shaft's hole: not dropped into it");
            Assert.AreEqual(0f, StorageBotRules.Ground(1f, 1.5f), "something high under it: not lifted onto it");
        }

        [Test]
        public void UpTheShaftItClimbsRightOnTheRungs()
        {
            var facing = Vector3.forward;
            var rungs = new Vector3(2f, -8f, 5f);
            var foot = new Vector3(2f, -8f, 2.8f);                 // the walkway at the ladder's foot
            var lobby = new Vector3(2f, 0.05f, 3.4f);              // the lobby's floor beside the hole
            var up = StorageBotRules.LadderWay(foot, lobby, rungs, facing);
            Assert.AreEqual(3, up.Length);
            var grip = rungs + facing * StorageBotRules.ClimbGrip;
            foreach (var p in new[] { up[0], up[1] })
                Assert.AreEqual(0f, new Vector2(p.x - grip.x, p.z - grip.z).magnitude, 1e-4f, "the climb is on the rungs, paws on them");
            Assert.AreEqual(foot.y, up[0].y, 1e-4f, "across to the ladder at its foot first");
            Assert.AreEqual(lobby.y, up[1].y, 1e-4f, "straight up to the lobby's floor");
            Assert.AreEqual(lobby, up[2], "then off where the link ends");
            var down = StorageBotRules.LadderWay(lobby, foot, rungs, facing);
            Assert.AreEqual(lobby.y, down[0].y, 1e-4f, "down: over to the rungs at the top first");
            Assert.AreEqual(foot.y, down[1].y, 1e-4f);
            Assert.AreEqual(foot, down[2]);
            Assert.AreEqual(0f, StorageBotRules.HopArc(0f, 2f));
            Assert.Greater(StorageBotRules.HopArc(0.5f, 2f), 0.2f, "over the hole it hops");
        }

        [Test]
        public void TheBackpacksBoardCoversItsGridWhateverItsRows()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/UI/StashBoard.prefab");
            Assert.IsNotNull(prefab);
            var go = Object.Instantiate(prefab);
            try
            {
                var board = go.GetComponent<Stealth.UI.StashBoard>();
                Assert.IsNull(go.transform.Find("StashAll"), "no \"put everything in\" strip: it is [G] in the hint stack");
                board.Root.sizeDelta = new Vector2(380f, 208f); // (an old scene's override of the paper's size)
                foreach (bool sell in new[] { true, false })
                    for (int rows = 1; rows <= TeamStash.MaxHeight; rows++)
                    for (int columns = 1; columns <= TeamStash.MaxWidth; columns++)
                    {
                        board.SetSell(sell);
                        board.Fit(columns, rows);
                        var paper = Corners(board.Root, board.Root);
                        var grid = Corners(board.GridBox, board.Root);
                        Assert.LessOrEqual(paper.yMin, grid.yMin - board.Pad * 0.5f, $"{rows} rows, burn {sell}: the paper reaches under the grid");
                        Assert.GreaterOrEqual(paper.xMax, grid.xMax + board.Pad * 0.5f, $"{rows} rows, burn {sell}: and right of it");
                        Assert.AreEqual(rows * Stealth.UI.InventoryUI.Cell, board.Grid.sizeDelta.y, 1e-3f);
                        Assert.AreEqual(columns * Stealth.UI.InventoryUI.Cell, board.Grid.sizeDelta.x, 1e-3f);
                        if (sell) Assert.GreaterOrEqual(Corners(board.SellZone, board.Root).xMin, grid.xMax, "the burn note right of the grid");
                        if (sell) Assert.GreaterOrEqual(paper.xMax, Corners(board.SellZone, board.Root).xMax, "the burn note on the paper");
                    }
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>A rect's corners in another's space.</summary>
        static Rect Corners(RectTransform rt, RectTransform space)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var a = space.InverseTransformPoint(c[0]);
            var b = space.InverseTransformPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        [Test]
        public void ThePrefabIsARaccoonWithItsPackAndEveryTake()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefab);
            Assert.IsNotNull(prefab, BotPrefab);
            Assert.IsNotNull(prefab.GetComponent<StorageBotPet>(), "it can be petted");
            var anim = prefab.GetComponent<StorageBotAnimator>();
            Assert.IsNotNull(anim.Animator, "the animator");
            var controller = anim.Animator.runtimeAnimatorController as AnimatorController;
            Assert.IsNotNull(controller);
            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
            foreach (var take in System.Enum.GetNames(typeof(StorageBotRules.Take)))
                CollectionAssert.Contains(states, take, $"the state {take}");
            Assert.IsTrue(controller.animationClips.All(c => c != null), "every take imported");
            var all = prefab.GetComponentsInChildren<Transform>(true);
            Transform Bone(string n) => all.FirstOrDefault(t => t.name == n);
            var head = Bone("head");
            var tail = Bone("tail4");
            var pack = Bone("pack");
            Assert.IsNotNull(head);
            Assert.IsNotNull(pack);
            Assert.Greater(prefab.transform.InverseTransformPoint(head.position).z, 0.15f, "it faces +Z");
            Assert.Less(prefab.transform.InverseTransformPoint(tail.position).z, 0f, "its tail behind");
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(renderer.sharedMaterial, "its fur");
            var backpack = all.FirstOrDefault(t => t.name == "Backpack");
            Assert.IsNotNull(backpack);
            Assert.AreSame(pack, backpack.parent, "the pack rides on its bone");
            var packed = prefab.GetComponentInChildren<RaccoonPack>();
            Assert.AreEqual(CarUpgrades.MaxLevelled + 1, packed.Levels.Length, "a pack for every level of the upgrade");
            Assert.IsTrue(packed.Levels.All(l => l != null), "every level modelled");
            Assert.AreEqual(packed.Levels.Length, packed.Loads.Length);
            Assert.AreEqual(packed.Levels.Length, packed.Boxes.Length);
            for (int i = 1; i < packed.Boxes.Length; i++)
                Assert.Greater(packed.Boxes[i].x * packed.Boxes[i].y * packed.Boxes[i].z, packed.Boxes[i - 1].x * packed.Boxes[i - 1].y * packed.Boxes[i - 1].z,
                    $"the pack of level {i} is bigger than the one before");
            Assert.AreEqual(TeamStash.MaxWidth * TeamStash.MaxHeight, packed.Pieces.Length, "a piece for every stash cell");
            Assert.GreaterOrEqual(prefab.GetComponentsInChildren<Dangle>(true).Length, 2, "charms swing");
            Assert.IsTrue(backpack.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).All(r => r.sharedMaterial != null), "every pack part painted");
            // on its paws: the lowest point of the pack stays above its back, the head is about half a metre up
            Assert.AreEqual(0.45f, prefab.transform.InverseTransformPoint(head.position).y, 0.12f);
        }
    }
}
