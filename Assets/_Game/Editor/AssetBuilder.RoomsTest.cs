using Stealth.Configs;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// The test rooms: the game with its own tuning (Data/Config/RoomsTest) — small procedural houses of three or four
    /// rooms on one floor, to tune rooms and generation in. One scene for both: the main menu's switch picks it
    /// (<see cref="Tuning.SetTestRooms"/>, through GameConfig.TestRooms); the main game is the default.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string RoomsTestScenePath = Root + "/Scenes/RoomsTest.unity"; // the old separate scene (removed)
        const string RoomsTestConfigDir = ConfigDir + "/RoomsTest";

        static void BuildRoomsTest()
        {
            var root = BuildRoomsTestConfigs();
            var main = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (main != null && main.TestRooms != root) { main.TestRooms = root; EditorUtility.SetDirty(main); }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RoomsTestScenePath) != null) AssetDatabase.DeleteAsset(RoomsTestScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Data/Config/RoomsTest: a GameConfig that shares every config of the game but the level — its own Level.asset,
        /// made once from the game's (then tuned by hand, never overwritten): one floor, a plain bar, no annex, rooms a
        /// bit bigger, 3–4 rooms (plans outside that are redrawn), no guards; and its own Run.asset without the hunter.
        /// </summary>
        static GameConfig BuildRoomsTestConfigs()
        {
            var main = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            var levelPath = RoomsTestConfigDir + "/Level.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(levelPath);
            if (level == null)
            {
                EnsureFolder(RoomsTestConfigDir);
                var source = main != null && main.Level != null ? AssetDatabase.GetAssetPath(main.Level) : null;
                if (source != null) AssetDatabase.CopyAsset(source, levelPath);
                level = AssetDatabase.LoadAssetAtPath<LevelConfig>(levelPath) ?? TuningAsset<LevelConfig>(levelPath);
                var b = level.Building;
                b.FloorsPerHouse = new[] { 1 };
                b.BaseRooms = 3.2f;            // ≈ 320 m² of floor: 3–4 rooms of 30..200 m² and maybe a corridor
                b.RoomsPerHouse = 0f;
                b.MaxRooms = 3.2f;
                b.AreaByPlayers = new[] { 1f };
                b.MassWeights = new[] { 1, 0, 0, 0, 0, 0 };
                b.AnnexChance = 0f;
                b.AtriumChance = 0f;
                b.CorridorChance = 0f;         // no spines lined with rooms (too many rooms for the test)…
                // …but a corridor across the house joining the rooms on its two sides (below)
                b.RotundaChance = 0f;
                b.RoomSizeScale = 1.4f;
                b.RoomCountMin = 3;
                b.RoomCountMax = 4;
                b.WindowsMin = 0;              // a window or two at most
                b.WindowsMax = 2;
                b.RoomAreaMin = 30f;           // every room 30..200 m²
                b.RoomAreaMax = 200f;
                b.CrossCorridorChance = 1f;    // a corridor between the rooms…
                b.MinCorridors = 1;            // …always
                b.DarkRoomsDim = true;         // every room lit: normal, dim (instead of dark) or flickering lamps
                b.DarkChance = 0.25f; b.DarkChancePerHouse = 0f;
                b.FlickerChance = 0.25f;
                var f = level.Features;
                f.KeyDoors = 0; f.Gadgets = false; f.ObjectivesFromHouse = 999; // only the loot table's items lie around
                level.Loot.Table = new[]
                {
                    new Stealth.Items.ItemCatalog.LootWeight { Item = "shovel", Weight = 5 },
                    new Stealth.Items.ItemCatalog.LootWeight { Item = "flashlight", Weight = 2 },
                    new Stealth.Items.ItemCatalog.LootWeight { Item = "battery", Weight = 3 },
                };
                var s = level.Security;
                s.Enemies = false;             // a quiet house: no guards at all
                s.Robots = 1; s.RobotsPerHouse = 0; s.RobotsPerExtraFloor = 0;
                s.Devices = 1; s.DevicesPerHouse = 0;
                s.Cameras = 1; s.CamerasPerHouse = 0;
                s.Lasers = 1; s.LasersPerHouse = 0;
                EditorUtility.SetDirty(level);
            }

            // its own run rules, made once from the game's: no hunter and no countdown
            var runPath = RoomsTestConfigDir + "/Run.asset";
            var run = AssetDatabase.LoadAssetAtPath<RunConfig>(runPath);
            if (run == null)
            {
                var source = main != null && main.Run != null ? AssetDatabase.GetAssetPath(main.Run) : null;
                if (source != null) AssetDatabase.CopyAsset(source, runPath);
                run = AssetDatabase.LoadAssetAtPath<RunConfig>(runPath) ?? TuningAsset<RunConfig>(runPath);
                run.Rules.Hunter = false;
                EditorUtility.SetDirty(run);
            }

            // its own sound mix, made once from the game's: no music in the house
            var audioPath = RoomsTestConfigDir + "/Audio.asset";
            var audio = AssetDatabase.LoadAssetAtPath<AudioConfig>(audioPath);
            if (audio == null)
            {
                var source = main != null && main.Audio != null ? AssetDatabase.GetAssetPath(main.Audio) : null;
                if (source != null) AssetDatabase.CopyAsset(source, audioPath);
                audio = AssetDatabase.LoadAssetAtPath<AudioConfig>(audioPath) ?? TuningAsset<AudioConfig>(audioPath);
                audio.Mix.LevelMusic = false;
                EditorUtility.SetDirty(audio);
            }

            var root = TuningAsset<GameConfig>(RoomsTestConfigDir + "/GameConfig.asset");
            if (main != null)
            {
                // everything but the level and the run rules is the game's own: tuning the game tunes the test scene too
                root.Player = main.Player; root.Items = main.Items;
                root.Perception = main.Perception; root.Styles = main.Styles;
            }
            root.Level = level;
            root.Run = run;
            root.Audio = audio;
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            return root;
        }
    }
}
