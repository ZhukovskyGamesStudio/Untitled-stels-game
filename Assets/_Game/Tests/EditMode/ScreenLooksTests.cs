using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// Looks decided by small pure rules: the furniture's screens (off, static, cracked), a ceiling grate's hinge side,
    /// the pieces that keep their front clear, what of his own body a player sees, and the styles the game keeps.
    /// </summary>
    public class ScreenLooksTests
    {
        [Test]
        public void TvsShowStaticAndComputersTheirBootMenuOrNoSignal()
        {
            foreach (ScreenKind kind in System.Enum.GetValues(typeof(ScreenKind)))
            {
                var counts = new int[System.Enum.GetValues(typeof(ScreenLook)).Length];
                for (int i = 0; i < 4000; i++) counts[(int)ScreenLooks.Pick(LevelRandom.Mix(12345, Salt.Screens, i), kind)]++;
                int On(ScreenLook look) => counts[(int)look];
                Assert.Greater(On(ScreenLook.Off), 1200, $"{kind}: many are off");
                Assert.Greater(On(ScreenLook.Cracked), 200, $"{kind}: some are broken");
                Assert.AreEqual(kind == ScreenKind.Tv, On(ScreenLook.Static) > 0, $"{kind}: static is a TV's");
                Assert.AreEqual(kind == ScreenKind.Computer, On(ScreenLook.NoSignal) > 800, $"{kind}: no signal");
                Assert.AreEqual(kind == ScreenKind.Computer, On(ScreenLook.Boot) > 800, $"{kind}: the boot menu");
                Assert.AreEqual(ScreenLooks.Pick(77, kind), ScreenLooks.Pick(77, kind), "the same on every peer");
                Assert.IsTrue(System.Enum.IsDefined(typeof(ScreenLook), ScreenLooks.Pick(-12345, kind)), "any seed, a negative one too");
            }
            Assert.AreEqual(ScreenKind.Tv, ScreenLooks.KindOf(PropKind.TvStand));
            Assert.AreEqual(ScreenKind.Computer, ScreenLooks.KindOf(PropKind.Desk));
            Assert.AreEqual(ScreenKind.Appliance, ScreenLooks.KindOf(PropKind.Treadmill));
            Assert.IsTrue(ScreenLooks.InUse(PropKind.Programmer), "what the players use stays lit");
            // a "no signal" box moves about and stays whole on its screen
            for (int k = 0; k < 40; k++)
            {
                var at = ScreenLooks.Drift(k);
                Assert.LessOrEqual(Mathf.Abs(at.x) + ScreenLooks.BoxHalf.x, 0.5001f);
                Assert.LessOrEqual(Mathf.Abs(at.y) + ScreenLooks.BoxHalf.y, 0.5001f);
            }
            // the static jumps about the texture, staying on it
            var seen = new HashSet<Vector2>();
            for (int k = 0; k < 50; k++)
            {
                var at = ScreenLooks.Jump(k);
                Assert.That(at.x, Is.InRange(0f, 1f));
                Assert.That(at.y, Is.InRange(0f, 1f));
                seen.Add(at);
            }
            Assert.Greater(seen.Count, 45, "a new place every step");
        }

        [Test]
        public void TheHouseHasMaterialsForEveryScreenLook()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            Assert.IsNotNull(art);
            Assert.AreEqual(1 + System.Enum.GetValues(typeof(ScreenLook)).Length, art.Screens.Length, "the models' screen, then every look");
            foreach (var m in art.Screens) Assert.IsNotNull(m);
            Assert.AreEqual(art.Screens.Length, art.Screens.Distinct().Count());
            Assert.IsFalse(art.Screens[1 + (int)ScreenLook.Off].IsKeywordEnabled("_EMISSION"), "a screen switched off does not glow");
            Assert.IsNotNull(art.Screens[1 + (int)ScreenLook.Static].GetTexture("_EmissionMap"), "static glows through its noise");
            Assert.IsNotNull(art.Screens[1 + (int)ScreenLook.Cracked].GetTexture("_BaseMap"), "the cracks");
            foreach (var look in new[] { ScreenLook.NoSignal, ScreenLook.Boot })
            {
                var picture = art.Screens[1 + (int)look].GetTexture("_EmissionMap");
                Assert.IsNotNull(picture, $"{look}: its picture");
                Assert.AreEqual(TextureWrapMode.Clamp, picture.wrapMode, $"{look}: once on a screen");
            }
        }

        [Test]
        public void APictureIsWholeOnEveryScreenTheRightWayRound()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            int faces = 0;
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                if (ScreenLooks.KindOf(kind) == ScreenKind.Appliance || art.Prop(kind) == null) continue;
                int here = 0;
                foreach (var r in art.Prop(kind).GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.sharedMaterial != art.Screens[0] || Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z)) < ScreenLooks.SmallScreen) continue;
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] v = mesh.vertices;
                    int[] t = mesh.triangles;
                    var uv = ScreenFaces.Fit(v, t);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector2 a = uv[t[i]], b = uv[t[i + 1]], c = uv[t[i + 2]];
                        if (Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) < 0.2f) continue; // an edge, a lamp
                        here++;
                        var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                        var right = Vector3.Cross(n, Vector3.up);
                        // v grows upwards, u to the right of whoever stands before it: from two corners that differ in each
                        foreach (var (p, q) in new[] { (t[i], t[i + 1]), (t[i + 1], t[i + 2]), (t[i + 2], t[i]) })
                        {
                            float up = v[q].y - v[p].y, across = Vector3.Dot(v[q] - v[p], right);
                            if (Mathf.Abs(up) > 0.05f) Assert.AreEqual(Mathf.Sign(up), Mathf.Sign(uv[q].y - uv[p].y), $"{kind}: upside down");
                            if (Mathf.Abs(across) > 0.05f) Assert.AreEqual(Mathf.Sign(across), Mathf.Sign(uv[q].x - uv[p].x), $"{kind}: mirrored");
                        }
                        foreach (var k in new[] { a, b, c }) Assert.IsTrue(k.x >= -0.001f && k.x <= 1.001f && k.y >= -0.001f && k.y <= 1.001f, $"{kind}: whole");
                    }
                }
                faces += here;
                if (kind is PropKind.Desk or PropKind.TvStand or PropKind.StandingDesk or PropKind.SupervisorDesk or PropKind.DoctorDesk or PropKind.MonitorWall or PropKind.ReceptionDesk)
                    Assert.Greater(here, 0, $"{kind}: a screen to show a picture on");
            }
            Assert.Greater(faces, 30);
        }

        [Test]
        public void TheStylesOwnScreensTakeThePictureToo()
        {
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var styles = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var families = styles.Styles.Select(st => st.Geometry.Family).Concat(styles.Houses.Select(h => h.Furniture)).ToHashSet(); // (those the game shows)
            var dead = new List<string>();
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                if (ScreenLooks.KindOf(kind) == ScreenKind.Appliance || art.Prop(kind) == null) continue;
                foreach (var r in art.Prop(kind).GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.sharedMaterial != art.Screens[0] || Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z)) < ScreenLooks.SmallScreen) continue;
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    bool Takes(Mesh m)
                    {
                        Assert.IsTrue(m.isReadable, $"{kind}: {AssetDatabase.GetAssetPath(m)} cannot be read in the game");
                        return ScreenFaces.Fit(m.vertices, m.triangles).Any(uv => uv.x > 0.95f && uv.y > 0.95f);
                    }
                    if (!Takes(mesh)) continue; // (little displays: lit as they are on every model)
                    foreach (var set in styles.Meshes.Where(m => families.Contains(m.Family)))
                        for (int i = 0; i < set.From.Length; i++)
                            if (set.From[i] == mesh && set.To[i] != null && !Takes(set.To[i])) dead.Add($"{kind} in {set.Family}");
                    foreach (var set in styles.StyleModels.Where(m => styles.Styles.Contains(m.Style)))
                        for (int i = 0; i < set.From.Length; i++)
                            if (set.From[i] == mesh && set.To[i] != null && !Takes(set.To[i])) dead.Add($"{kind} in {set.Style.name}");
                }
            }
            CollectionAssert.IsEmpty(dead, "screens whose every face is too small or curved for a picture: dark");
        }

        [Test]
        public void NoScreenIsLeftHangingWhenAHousesOwnFurnitureStandsIn()
        {
            // a piece replaced as a whole: its glowing parts stay only where the stand-in has no screen of its own, and then
            // only a lamp's bulb — never a screen's sheet in the air
            var art = AssetDatabase.LoadAssetAtPath<LevelArt>("Assets/_Game/Data/LevelArt.asset");
            var styles = Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles;
            var left = new List<string>();
            foreach (PropKind kind in System.Enum.GetValues(typeof(PropKind)))
            {
                var prefab = art.Prop(kind);
                if (prefab == null || prefab.GetComponentInChildren<Stealth.Items.Storage>(true) != null) continue;
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToHashSet();
                foreach (var p in styles.Props.Where(p => p.Family is ModelFamily.Realistic or ModelFamily.Derelict && !p.HideGlow && meshes.Contains(p.Key)))
                    foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Eye") && r.sharedMaterial == art.Screens[0]))
                    {
                        // is it inside (or on) the stand-in?
                        var stand = p.Prefab.GetComponentsInChildren<MeshFilter>(true);
                        bool any = false;
                        var b = new Bounds();
                        foreach (var f in stand)
                        {
                            var m = Matrix4x4.TRS(p.Position, Quaternion.identity, p.Scale) * Matrix4x4.TRS(f.transform.localPosition, f.transform.localRotation, f.transform.localScale);
                            for (int k = 0; k < 8; k++)
                            {
                                var e = f.sharedMesh.bounds.extents;
                                var q = m.MultiplyPoint3x4(f.sharedMesh.bounds.center + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z));
                                if (!any) { b = new Bounds(q, Vector3.zero); any = true; } else b.Encapsulate(q);
                            }
                        }
                        b.Expand(0.02f);
                        var eye = r.bounds;
                        if (!b.Contains(eye.min) || !b.Contains(eye.max)) left.Add($"{kind} ({p.Family}): {r.name} {eye.size:F2}");
                    }
            }
            CollectionAssert.IsEmpty(left);
        }

        [Test]
        public void ACeilingGratesLidHangsAwayFromTheDoorway()
        {
            float ceiling = LevelMetrics.WallHeight;
            var grate = new Vector3(5.5f, ceiling, 3.5f);
            Doorway Door(float x, float z, float y = 0f) => new() { Kind = EdgeType.Door, Position = new Vector3(x, y, z), Rotation = Quaternion.identity, Width = 1.6f };
            // the doorway behind the grate (−Z): as built the lid would hang across it; it is turned to hang on the far side
            float yaw = VentPlan.LidYaw(grate, new[] { Door(5.5f, 2.5f) });
            var hinge = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            Assert.Greater(hinge.z, 0.9f, "the hinge on the side away from the door");
            // a doorway to the +X: the hinge to −X
            yaw = VentPlan.LidYaw(grate, new[] { Door(6.6f, 3.5f) });
            Assert.Less((Quaternion.Euler(0f, yaw, 0f) * Vector3.back).x, -0.9f);
            // a doorway on another storey says nothing; no doorways: as built
            Assert.AreEqual(0f, VentPlan.LidYaw(grate, new[] { Door(5.5f, 2.5f, LevelMetrics.FloorHeight) }));
            Assert.AreEqual(0f, VentPlan.LidYaw(grate, new Doorway[0]));
        }

        [Test]
        public void WhatIsLookedAtFromItsFrontKeepsItClear()
        {
            Assert.IsTrue(Furnisher.NeedsFront(PropKind.LogoTotem), "a totem's screen is not put against the side of a cupboard");
            Assert.IsTrue(Furnisher.NeedsFront(PropKind.Cabinet), "nor a chest's drawers");
            Assert.IsFalse(Furnisher.NeedsFront(PropKind.Plant));
            foreach (var k in Furnisher.Faced)
            {
                Assert.IsFalse(Furnisher.Opens.Contains(k), $"{k}: either it opens or it is looked at");
                Assert.Greater(Furnisher.OpenReach(k), Furnisher.OpenReach(PropKind.Cabinet), $"{k}: seen from a step back");
            }
        }

        [Test]
        public void ThePlayerLooksOutOfHisOwnBody()
        {
            // what is on the head is not seen from inside it (it still casts its shadow); the rest of the body is
            foreach (var n in new[] { "Hair.hair", "Outfit.hair", "Hair.beard", "BodyClothes.head", "EyeDetails.eyes", "Sclera" })
                Assert.IsTrue(HumanMath.OnTheHead(n), n);
            foreach (var n in new[] { "BodyClothes.top", "Outfit.bottom", "Outfit.feet", "AccentHead", "AccentBand.top", "" })
                Assert.IsFalse(HumanMath.OnTheHead(n), n);
            // the body hangs from the eyes: moved so that its head (leant forward by a take) is at them
            var eyes = new Vector3(2f, 1.6f, 3f);
            var head = new Vector3(2f, 1.42f, 3.25f);
            var shift = HumanMath.EyeShift(eyes, head, Vector3.forward, 1f);
            var at = head + shift + Vector3.up * HumanMath.EyesUp + Vector3.forward * HumanMath.EyesAhead;
            Assert.Less((at - eyes).magnitude, 1e-4f, "the eyes of the body are at the camera");
            Assert.Less(shift.z, 0f, "a body leaning forward is moved back under the eyes");
            Assert.LessOrEqual(HumanMath.EyeShift(eyes, eyes + Vector3.forward * 5f, Vector3.forward, 1f).magnitude, HumanMath.EyeReach + 1e-4f, "never far");
            // looking out of it, the body stands behind the eyes, its feet on the floor whatever its height
            var own = HumanMath.OwnShift(eyes, head, Vector3.forward, 1f, planted: true);
            Assert.AreEqual(0f, own.y, 1e-5f, "standing: the feet stay on the floor");
            Assert.Less((head + own).z, eyes.z - 0.25f, "the neck is behind the view: no look down the collar");
            Assert.AreNotEqual(0f, HumanMath.OwnShift(eyes, head, Vector3.forward, 1f, planted: false).y, "in a seat: at the eyes' height");
            // each person's own view: a skin without a head, an arm for a long thing and one for a compact one
            foreach (var human in Resources.Load<GameConfig>(GameConfig.ResourcePath).Styles.PlayerHumans)
            {
                var rig = human.GetComponent<HumanRig>();
                Assert.IsNotNull(rig.Skin, human.name);
                Assert.IsNotNull(rig.OwnSkin, human.name);
                Assert.IsFalse(rig.OwnSkin.enabled, "only its owner sees it");
                Assert.Less(rig.OwnSkin.sharedMesh.triangles.Length, rig.Skin.sharedMesh.triangles.Length * 0.75f, $"{human.name}: the head is cut out");
                Assert.Greater(rig.OwnSkin.sharedMesh.triangles.Length, 3000, $"{human.name}: the hands are not");
                foreach (var arm in new[] { rig.ViewFist, rig.ViewCup })
                {
                    Assert.IsNotNull(arm, human.name);
                    var b = new Bounds();
                    foreach (var f in arm.GetComponentsInChildren<MeshFilter>()) b.Encapsulate(f.sharedMesh.bounds);
                    Assert.That(b.size.magnitude, Is.InRange(0.3f, 0.9f), $"{arm.name}: an arm");
                    Assert.IsTrue(arm.GetComponentsInChildren<Transform>().Any(t => t.name == "AccentHead"), $"{arm.name}: its hand");
                }
            }
            // his own flashlight leaves his own body out
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab").GetComponent<PlayerController>();
            Assert.AreEqual("OwnBody", LayerMask.LayerToName(PlayerController.OwnBodyLayer));
            Assert.AreEqual(0, player.Flashlight.cullingMask & (1 << PlayerController.OwnBodyLayer), "no shadows of the owner's body in his own beam");
            Assert.IsNotNull(player.EyeGlow);
            Assert.AreEqual(LightShadows.None, player.EyeGlow.shadows);
            Assert.IsNull(player.EyeGlow.GetComponent<LightSource>(), "robots do not see by it");
        }

        [Test]
        public void TheGameKeepsTwoStyles()
        {
            var config = AssetDatabase.LoadAssetAtPath<StyleConfig>("Assets/_Game/Data/Styles/Styles.asset");
            Assert.IsNotNull(config);
            CollectionAssert.AreEquivalent(new[] { "S06_Storybook", "S16_Derelict" }, config.Styles.Select(s => s.name).ToArray());
            Assert.AreEqual("S16_Derelict", config.Default.name);
            // a dark room is dark: the house-wide fill is small, the lamps light their own rooms
            var quiet = config.Styles.First(s => s.name == "S16_Derelict");
            Assert.Less(quiet.World.Ambient.maxColorComponent, 0.13f, "a room with its lamps off needs a flashlight");
            Assert.Greater(quiet.World.CeilingGlow, 0f, "a lit room's own lamps light its ceiling");
        }
    
        /// <summary>The pieces the players use had plain glowing blue sheets for screens: nobody had given them pictures.</summary>
        [Test]
        public void TheScreensOfWhatThePlayersUseShowSomething()
        {
            var console = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Props/Programmer.prefab");
            var monitors = console.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("EyeScreen")).ToArray();
            Assert.IsNotEmpty(monitors, "the programming console has no monitors of its own");
            foreach (var r in monitors)
            {
                Assert.AreEqual("ProgrammerScreen", r.sharedMaterial.name);
                Assert.IsNotNull(r.sharedMaterial.GetTexture("_EmissionMap"), "its monitors glow with no picture");
                var uv = r.GetComponent<MeshFilter>().sharedMesh.uv;
                // three monitors, each its own third of the picture: their corners lie on the thirds' borders
                foreach (float border in new[] { 0f, 1f / 3f, 2f / 3f, 1f })
                    Assert.IsTrue(uv.Any(t => Mathf.Abs(t.x - border) < 0.02f), $"no monitor's side at {border:0.00} of the picture");
                Assert.IsTrue(uv.All(t => t.x > -0.001f && t.x < 1.001f && t.y > -0.001f && t.y < 1.001f), "a monitor reads past the picture");
            }
            Assert.IsTrue(console.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("Eye") && !r.name.StartsWith("EyeScreen")),
                "the dock lamps are lamps, not part of the monitors");

            var table = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Props/MedTable.prefab");
            Assert.IsTrue(table.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("EyeSign") && r.sharedMaterial.name == "MedGlow"), "the medical cross is green");
            Assert.GreaterOrEqual(ScreenLooks.DisplayOf(PropKind.MedTable), 0, "the medical table's screen shows the patient's vitals");
            Assert.IsTrue(ScreenLooks.InUse(PropKind.MedTable), "…and is never switched off");
        }
    }
}
