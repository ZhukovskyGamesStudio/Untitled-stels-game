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
            if (art.Crate == crate && art.MedTable == med && art.Workbench == bench && art.Programmer == programmer && art.IllusionBlock == illusion) return;
            art.Crate = crate;
            art.MedTable = med;
            art.Workbench = bench;
            art.Programmer = programmer;
            art.IllusionBlock = illusion;
            EditorUtility.SetDirty(art);
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
                string file = s.Id == "speaker" ? "item_domino" : $"item_{s.Id}"; // the speaker's piece in a grid is a domino
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
