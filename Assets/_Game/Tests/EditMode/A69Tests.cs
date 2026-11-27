using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>What the play of the A68 build turned up: parts lost by the prefabs, screens in the way, the hand on what does not open, glowing beds, dead-looking plates.</summary>
    public class A69Tests
    {
        static GameObject Prop(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Props/{name}.prefab");

        [Test]
        public void NoModelHasTwoPartsOfOneName()
        {
            // a second merge("Body") in a model script exports "Body.001": the prefab keeps one of them and the other is gone
            // (the kitchen counter's microwave stood without its casing)
            var twice = new Regex(@"(\.\d{3}| \d+)$");
            var wrong = AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Game/Art/Models/Props", "Assets/_Game/Art/Models/Level" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .SelectMany(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true)
                    .Where(mf => twice.IsMatch(mf.name)).Select(mf => $"{System.IO.Path.GetFileName(path)}: {mf.name}"))
                .ToList();
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void NothingHangsOverTheKitchenCountersWorktop()
        {
            // its built-in microwave lost its casing (the glass and the display hung in the air): the worktop is bare now,
            // what stands on it are pieces of their own
            foreach (var r in Prop("KitchenCounter").GetComponentsInChildren<MeshRenderer>(true))
                Assert.Less(r.bounds.max.y, 0.96f, $"{r.name} stands over the worktop");
        }

        [Test]
        public void APlayersBodyGoesThroughADesksScreens([Values("Desk", "SupervisorDesk")] string name)
        {
            var prefab = Prop(name);
            var passable = prefab.GetComponent<PassableForPlayers>();
            Assert.IsNotNull(passable, "no PassableForPlayers");
            Assert.IsNotEmpty(passable.Parts);
            Assert.IsTrue(passable.Parts.All(c => c != null && c.name.StartsWith("Eye")), "only the screens");
            Assert.IsTrue(prefab.GetComponentsInChildren<Collider>(true).Any(c => !passable.Has(c)), "the desk itself is solid");
            // (that a body really passes is physics at play: Physics.IgnoreCollision keeps no state in an edit mode test)
        }

        [Test]
        public void APlayersBodyGoesThroughATerminalsMonitors()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Props/Terminal.prefab");
            var passable = prefab.GetComponent<PassableForPlayers>();
            Assert.IsNotNull(passable, "no PassableForPlayers");
            // the cases, the stands and the screens of the console's monitors: everything over the desk's top but the low panel
            Assert.AreEqual(3, passable.Parts.Length, "the monitors' cases, their stands and their screens");
            Assert.IsTrue(passable.Parts.All(c => c != null));
            var console = prefab.transform.Find("Console");
            foreach (var c in console.GetComponentsInChildren<MeshCollider>(true))
                if (!passable.Has(c)) Assert.Less(c.sharedMesh.bounds.max.y, 0.95f, $"{c.name} stands solid over the desk: in the way into the duct");
            Assert.IsTrue(console.GetComponentsInChildren<Collider>(true).Any(c => !passable.Has(c)), "the desk itself is solid");
        }

        [Test]
        public void TheHandShowsOnlyOnWhatOpens()
        {
            var go = Object.Instantiate(Prop("MedicineCabinet"));
            try
            {
                var storage = go.GetComponent<Storage>();
                Assert.IsNotNull(storage);
                storage.Configure(0);
                var all = go.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var doors = go.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("BodyDoor")).ToArray();
                Assert.IsNotEmpty(doors);
                var front = go.transform.forward;
                foreach (var d in doors)
                {
                    var from = d.bounds.center + front * 1.2f;
                    Assert.GreaterOrEqual(storage.PartUnder(from, -front), 0, $"{d.name}: looked at, it answers");
                }
                // the glass top: over the doors, nothing opens there
                float top = doors.Max(d => d.bounds.max.y);
                Assert.Greater(all.max.y, top + 0.4f, "the cabinet has a top that does not open");
                var glass = new Vector3(all.center.x, (top + all.max.y) / 2f, all.center.z) + front * 1.2f;
                Assert.AreEqual(-1, storage.PartUnder(glass, -front), "the glass top offers nothing");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ThePrintersBedDoesNotGlow()
        {
            var eyes = Prop("Printer3d").GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Eye")).ToArray();
            Assert.IsNotEmpty(eyes, "its display");
            foreach (var e in eyes)
                Assert.Less(Mathf.Max(e.bounds.size.x, e.bounds.size.z), 0.2f, "a sheet of light: the bed is no screen");
        }

        [Test]
        public void APressurePlateShowsThatItIsArmed()
        {
            var plate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Sensors/SensorPlate.prefab").GetComponent<PressurePlate>();
            Assert.IsFalse(plate.GlowEyes, "it does not see");
            Assert.That(plate.StateLine, Is.InRange(1f, 2f), "its line really glows: armed and dead must not look alike");
            Assert.IsNotEmpty(plate.Status.Renderers, "nothing to show its state on");
            foreach (var path in new[] { "Sensors/SensorLidar", "Robots/RobotVacuum", "Robots/RobotSpider" }) // the others with no eye and no ear
            {
                var d = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/{path}.prefab").GetComponent<Detector>();
                Assert.IsFalse(d.GlowEyes || d.GlowEars, path);
                Assert.That(d.StateLine, Is.InRange(0.8f, 2f), $"{path}: armed and hacked must not look alike");
                Assert.IsNotEmpty(d.Status.Renderers, $"{path}: nothing to show its state on");
            }
        }

        [Test]
        public void TheStorageRoomHasAnExitButtonInsideBesideItsDoor()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art.ExitButton, "no ExitButton prefab");
            var button = art.ExitButton.GetComponent<ExitButton>();
            Assert.IsNotNull(button.Cap);
            Assert.IsNotNull(button.Lamp);
            Assert.AreEqual(UseKind.ExitButton, button.Kind);
            Assert.AreEqual(0f, button.HoldTime, "one press, no hold");
            // beside the doorway, on the room's side of the wall, at a hand's height
            var door = new Vector3(10f, 0f, 5f);
            var inward = Vector3.forward;
            var along = Vector3.right;
            float clear = LevelMetrics.ClearWidth(LevelMetrics.DoorWidth);
            foreach (float side in new[] { 1f, -1f })
            {
                var at = ExitButton.Spot(door, inward, along, clear, side);
                Assert.IsTrue(AutoDoor.IsInside(door, inward, at), "inside the room");
                Assert.Greater(Mathf.Abs(at.x - door.x), clear / 2f + 0.15f, "clear of the door's frame");
                Assert.That(at.y, Is.InRange(1f, 1.3f));
                Assert.That(at.z - door.z, Is.InRange(LevelMetrics.WallThickness / 2f, LevelMetrics.WallThickness / 2f + 0.05f), "on the wall's face");
            }
            // built with a house that locks its storage room: one button, in that room, within reach of the door
            int found = 0;
            foreach (var (seed, house) in new[] { (99991, 4), (7, 3), (42, 5) })
            {
                var l = TestHouses.Get(seed, house);
                ActiveLevel.Load(l, art);
                var buttons = ActiveLevel.Root.GetComponentsInChildren<ExitButton>();
                if (!StorageRooms.Locked(l)) { Assert.IsEmpty(buttons, $"seed {seed}: no locked storage room, no button"); continue; }
                Assert.AreEqual(1, buttons.Length, $"seed {seed}");
                found++;
                var b = buttons[0];
                Assert.AreEqual(l.StorageDoor, b.Door);
                Assert.AreEqual(l.StorageRoom, l.Plan.RoomAt(b.transform.position + b.transform.forward * 0.4f)?.Id, $"seed {seed}: the button is not in the storage room");
                var d = ActiveLevel.Root.GetComponentsInChildren<AutoDoor>().First(a => a.Storage); // (no OnEnable in an edit mode test: AutoDoor.All is empty)
                Assert.Less((b.transform.position - d.transform.position).magnitude, 2.2f, "by the door");
                // the door itself is no lock to work on any more, from either side
                Assert.IsFalse(((IReachTarget)d).CanReach(b.transform.position + b.transform.forward * 0.5f));
            }
            Assert.Greater(found, 0, "none of the test houses locks its storage room");
        }

        [Test]
        public void FeetAndHandsOnALadderSoundAsTheClimbersOwnGetThere()
        {
            // one cycle of the take: each foot once, each hand once, a foot first and the opposite hand right after it
            Assert.AreEqual((2, 2), Stealth.Player.HumanMath.ClimbContacts(0f, 1f));
            Assert.AreEqual((1, 0), Stealth.Player.HumanMath.ClimbContacts(0.2f, 0.28f), "the foot is on its rung…");
            Assert.AreEqual((0, 1), Stealth.Player.HumanMath.ClimbContacts(0.28f, 0.35f), "…then the hand closes");
            Assert.AreEqual((1, 1), Stealth.Player.HumanMath.ClimbContacts(0.7f, 0.85f), "the other pair, half a cycle on");
            Assert.AreEqual((0, 0), Stealth.Player.HumanMath.ClimbContacts(0.4f, 0.6f), "between the steps: silence");
            Assert.AreEqual((0, 0), Stealth.Player.HumanMath.ClimbContacts(0.3f, 0.3f), "hanging still is silent");
            Assert.AreEqual((2, 2), Stealth.Player.HumanMath.ClimbContacts(1f, 0f), "the way down: the same moments, played back");
            Assert.AreEqual((1, 0), Stealth.Player.HumanMath.ClimbContacts(-0.7f, -0.8f), "below where the climb began, too");
            Assert.AreEqual((0, 0), Stealth.Player.HumanMath.ClimbContacts(0f, 9f), "another ladder: no drum roll");
            // frame by frame nothing is lost and nothing doubles: 3 m of ladder is five cycles
            float phase = 0f;
            int feet = 0, hands = 0;
            for (int frame = 0; frame < 300; frame++)
            {
                float next = phase + Stealth.Player.HumanMath.ClimbCycles(Stealth.Player.HumanMath.ClimbRate(0.5f), 0.02f);
                var (f, h) = Stealth.Player.HumanMath.ClimbContacts(phase, next);
                feet += f;
                hands += h;
                phase = next;
            }
            Assert.AreEqual(3f / Stealth.Player.HumanMath.ClimbCycle, phase, 0.01f, "the take's own speed: a cycle every 0.6 m");
            Assert.AreEqual(10, feet, "a foot on a rung every 0.3 m");
            Assert.AreEqual(10, hands);
            Assert.Less(new Stealth.Configs.AudioMix().LadderHands, new Stealth.Configs.AudioMix().LadderFeet, "a hand is softer than a boot");
        }

        [Test]
        public void EveryRungOfALadderSoundsAndRobotsHearIt()
        {
            float carried = 0f;
            int rungs = 0;
            for (int frame = 0; frame < 100; frame++) rungs += Stealth.Audio.AudioRules.Rungs(ref carried, 0.03f); // 3 m up
            Assert.That(rungs, Is.InRange(9, 10), "a rung every 0.3 m");
            carried = 0f;
            rungs = 0;
            for (int frame = 0; frame < 100; frame++) rungs += Stealth.Audio.AudioRules.Rungs(ref carried, -0.03f);
            Assert.That(rungs, Is.InRange(9, 10), "and on the way down");
            carried = 0f;
            for (int frame = 0; frame < 100; frame++) Assert.AreEqual(0, Stealth.Audio.AudioRules.Rungs(ref carried, 0f), "hanging still is silent");
            carried = 0f;
            Assert.LessOrEqual(Stealth.Audio.AudioRules.Rungs(ref carried, 40f), 3, "a teleport is no drum roll");
            Assert.Greater(new Stealth.Player.NoiseSettings().LadderRadius, 0f, "robots hear a climb");
            var bank = AssetDatabase.LoadAssetAtPath<Stealth.Audio.SoundBank>("Assets/_Game/Data/SoundBank.asset");
            Assert.AreEqual(4, bank.LadderFoot.Length);
            Assert.AreEqual(4, bank.LadderHand.Length);
            Assert.IsTrue(bank.LadderFoot.Concat(bank.LadderHand).All(c => c != null));
            foreach (var clip in new[] { bank.TerminalGranted, bank.TerminalHackDone, bank.ConsoleSolved, bank.VaultUnlocked }) Assert.IsNotNull(clip, "a terminal's work ends with a sound");
        }
    }
}
