using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>
        /// The special places of a house (Tools/Blender/poi.py): a crate to search, the med table, the workbench, the robot
        /// programming console, the illusion wall of a hidden room. Filled into an existing LevelArt without overwriting it.
        /// </summary>
        static void BuildFeatureArt(LevelArt art)
        {
            string p = PropPrefabs + "/";
            var crate = Prefab(p + "Crate.prefab", () =>
            {
                var go = PropObject("Crate", "crate", "Wood", "Metal", "Metal");
                go.AddComponent<SearchCrate>().Lid = Find(go, "AccentLid");
                return go;
            });
            var med = Prefab(p + "MedTable.prefab", () =>
            {
                var go = PropObject("MedTable", "med_table", "Ceiling", "Metal", "PropScreen");
                go.AddComponent<MedTable>();
                return go;
            });
            var bench = Prefab(p + "Workbench.prefab", () =>
            {
                var go = PropObject("Workbench", "workbench", "Wood", "Metal", "LampBulb");
                go.AddComponent<Workbench>();
                return go;
            });
            var programmer = Prefab(p + "Programmer.prefab", () => PropObject("Programmer", "programmer_console", "PropBody", "PropAccent", "PropScreen"));
            var illusion = Prefab($"{LevelPrefabs}/IllusionBlock.prefab", () =>
            {
                // a wall block to look at, nothing to bump into; robots keep out (they would give it away)
                var go = Block("IllusionBlock", M("Wall"), collider: false);
                var volume = go.AddComponent<NavMeshModifierVolume>();
                volume.center = Vector3.zero;
                volume.size = new Vector3(1.6f, 1f, 1.6f);
                volume.area = 1; // Not Walkable
                return go;
            });
            var console = Prefab(p + "VaultConsole.prefab", BuildVaultConsole);
            var cage = Prefab(p + "Cage.prefab", BuildCage);
            var vaultPanel = Prefab($"{LevelPrefabs}/VaultPanel.prefab", BuildVaultPanel);
            if (art.Crate == crate && art.MedTable == med && art.Workbench == bench && art.Programmer == programmer && art.IllusionBlock == illusion
                && art.VaultConsole == console && art.Cage == cage && art.VaultPanel == vaultPanel) return;
            art.Crate = crate;
            art.MedTable = med;
            art.Workbench = bench;
            art.Programmer = programmer;
            art.IllusionBlock = illusion;
            art.VaultConsole = console;
            art.Cage = cage;
            art.VaultPanel = vaultPanel;
            EditorUtility.SetDirty(art);
        }

        /// <summary>
        /// A lock panel of the vault on a wall (the root on the wall's face, +Z into the room): a steel box at hand height,
        /// a screen (red, green once solved), a strip of keys under it. No collider: it hangs flat on the wall.
        /// </summary>
        static GameObject BuildVaultConsole()
        {
            var root = new GameObject("VaultConsole");
            var steel = M("Metal");
            Cube(root.transform, "Body", new Vector3(0f, 0f, 0.035f), new Vector3(0.46f, 0.56f, 0.07f), steel, false);
            Cube(root.transform, "Frame", new Vector3(0f, 0.1f, 0.071f), new Vector3(0.36f, 0.24f, 0.004f), M("PropBody"), false);
            var screen = Cube(root.transform, "EyeScreen", new Vector3(0f, 0.1f, 0.075f), new Vector3(0.32f, 0.2f, 0.006f), M("LockLamp"), false);
            for (int i = 0; i < 4; i++)
                Cube(root.transform, $"Key{i}", new Vector3(-0.135f + i * 0.09f, -0.14f, 0.075f), new Vector3(0.07f, 0.06f, 0.012f), M("Keypad"), false);
            Cube(root.transform, "Stripe", new Vector3(0f, -0.25f, 0.072f), new Vector3(0.46f, 0.04f, 0.004f), M("DoorFrame"), false);
            var console = root.AddComponent<VaultConsole>();
            console.Screen = screen.GetComponent<Renderer>();
            return root;
        }

        /// <summary>The vault door's plate over the doorway (+Z out of the wall): three lamps, one per lock panel.</summary>
        static GameObject BuildVaultPanel()
        {
            var root = new GameObject("VaultPanel");
            Cube(root.transform, "Plate", new Vector3(0f, 0f, 0.01f), new Vector3(0.74f, 0.2f, 0.02f), M("Metal"), false);
            var panel = root.AddComponent<VaultPanel>();
            panel.Lamps = new Renderer[3];
            for (int i = 0; i < 3; i++)
                panel.Lamps[i] = Cube(root.transform, $"Lamp{i}", new Vector3((i - 1) * 0.22f, 0f, 0.025f), new Vector3(0.14f, 0.1f, 0.02f), M("LockLamp"), false).GetComponent<Renderer>();
            return root;
        }

        /// <summary>
        /// The hostage's cage, 2 x 2 m (its door on +Z): a floor plate, corner posts, bars on every side and over the top,
        /// the door's bars a part of their own (hidden once open), a person inside; box colliders keep everyone out.
        /// </summary>
        static GameObject BuildCage()
        {
            var root = new GameObject("Cage");
            float half = HouseFeatures.CageSize / 2f, h = 2.2f;
            var steel = M("Metal");
            Cube(root.transform, "Floor", new Vector3(0f, 0.03f, 0f), new Vector3(2f * half, 0.06f, 2f * half), steel, false);
            foreach (var (x, z) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
                Cube(root.transform, "Post", new Vector3(x * (half - 0.04f), h / 2f, z * (half - 0.04f)), new Vector3(0.08f, h, 0.08f), steel, false);
            Cube(root.transform, "Top", new Vector3(0f, h, 0f), new Vector3(2f * half, 0.05f, 2f * half), M("DoorFrame"), false);
            const float step = 0.18f;
            int bars = Mathf.FloorToInt((2f * half - 0.2f) / step);
            for (int i = 0; i <= bars; i++)
            {
                float t = -half + 0.1f + i * (2f * half - 0.2f) / bars;
                Cube(root.transform, "BarBack", new Vector3(t, h / 2f, -half + 0.04f), new Vector3(0.03f, h, 0.03f), steel, false);
                Cube(root.transform, "BarLeft", new Vector3(-half + 0.04f, h / 2f, t), new Vector3(0.03f, h, 0.03f), steel, false);
                Cube(root.transform, "BarRight", new Vector3(half - 0.04f, h / 2f, t), new Vector3(0.03f, h, 0.03f), steel, false);
            }
            var door = new GameObject("Door");
            door.transform.SetParent(root.transform, false);
            door.AddComponent<KeepUnmerged>();
            for (int i = 0; i <= bars; i++)
            {
                float t = -half + 0.1f + i * (2f * half - 0.2f) / bars;
                Cube(door.transform, "Bar", new Vector3(t, h / 2f, half - 0.04f), new Vector3(0.03f, h, 0.03f), steel, false);
            }
            Cube(door.transform, "Lock", new Vector3(half - 0.35f, 1.1f, half - 0.02f), new Vector3(0.16f, 0.2f, 0.08f), M("LockLamp"), false);
            // the walls of bars and the door are solid
            void Wall(GameObject on, Vector3 c, Vector3 size)
            {
                var b = on.AddComponent<BoxCollider>();
                b.center = c;
                b.size = size;
            }
            Wall(root, new Vector3(0f, h / 2f, -half + 0.04f), new Vector3(2f * half, h, 0.1f));
            Wall(root, new Vector3(-half + 0.04f, h / 2f, 0f), new Vector3(0.1f, h, 2f * half));
            Wall(root, new Vector3(half - 0.04f, h / 2f, 0f), new Vector3(0.1f, h, 2f * half));
            Wall(root, new Vector3(0f, h, 0f), new Vector3(2f * half, 0.08f, 2f * half));
            var doorCollider = door.AddComponent<BoxCollider>();
            doorCollider.center = new Vector3(0f, h / 2f, half - 0.04f);
            doorCollider.size = new Vector3(2f * half, h, 0.1f);
            // the person inside: a player's model, standing at the back, facing the door
            var hostage = Model("player");
            hostage.name = "Hostage";
            hostage.transform.SetParent(root.transform, false);
            hostage.transform.localPosition = new Vector3(0f, 0.06f, -0.25f);
            Paint(hostage, "PlayerBody", "PlayerAccent", "PlayerGlasses");
            hostage.AddComponent<KeepUnmerged>();
            var cage = root.AddComponent<Cage>();
            cage.Door = door;
            cage.DoorCollider = doorCollider;
            cage.Hostage = hostage;
            return root;
        }

        /// <summary>The tools among the items: their meshes (one per shape) and the lure speaker's own model.</summary>
        static void FillToolVisuals(ItemVisuals visuals)
        {
            var lure = Prefab($"{PrefabsDir}/Items/LureSpeakerModel.prefab", () =>
            {
                var m = Model("lure_speaker");
                m.name = "LureSpeakerModel";
                Paint(m, "PropBody", "Metal", "LampBulb");
                return m;
            });
            if (visuals.Meshes != null && visuals.Meshes.Length == ItemCatalog.Shapes.Length && visuals.Models != null && visuals.Models.Length == ItemCatalog.Shapes.Length
                && visuals.Models[ItemCatalog.Speaker] == lure) return;
            visuals.Meshes = ItemCatalog.Shapes.Select(s =>
            {
                // the speaker's piece in a grid is a domino; the cage's key looks like the door keys
                string file = s.Id == "speaker" ? "item_domino" : s.Id == "cagekey" ? "item_key" : $"item_{s.Id}";
                return AssetDatabase.LoadAllAssetsAtPath(ModelPath(file)).OfType<Mesh>().First();
            }).ToArray();
            visuals.Models = new GameObject[ItemCatalog.Shapes.Length];
            visuals.Models[ItemCatalog.Speaker] = lure;
            EditorUtility.SetDirty(visuals);
        }

        /// <summary>The warden: a tall sentinel on wheels with a floodlight head; it watches a doorway and raises the alarm.</summary>
        static GameObject BuildWarden(RadioRipple ripple)
        {
            Mat("Robots", "WardenBody", new Color(0.18f, 0.42f, 0.62f), smoothness: 0.45f);
            var go = MobileRobot("RobotWarden", "robot_warden", "WardenBody", 0.42f, new Vector3(0, 1.55f, 0.27f), ripple, (s, t, anim) =>
            {
                // sharp eyes on the doorway, keen ears (that is how it is drawn away), no fighting: it calls the others
                s.ViewDistance = 10f; s.ViewAngle = 110f; s.DetectionSpeed = 1.1f; s.PeripheralDistance = 3f;
                s.HearingMultiplier = 1.8f; s.ContactDistance = 1.2f;
                t.CanAttack = false;
                t.AlarmOnDetect = true;
                t.AlarmRadius = 30f;
                t.CanUseStairs = false;
                t.PatrolSpeed = 1.1f; t.InvestigateSpeed = 2.6f; t.ChaseSpeed = 2.6f;
                t.WarningTime = 0.8f;
                t.PatrolWaitMin = 1.5f; t.PatrolWaitMax = 3f;
            });
            // the floodlight and the beacon turn with the head
            var head = Find(go, "AccentHead" + RobotAnimator.PivotSuffix) ?? Find(go, "AccentHead");
            if (head != null)
                foreach (var n in new[] { "EyeLens", "EyeBeacon" })
                {
                    var part = Find(go, n);
                    if (part != null) part.SetParent(head, true);
                }
            return go;
        }
    }
}
