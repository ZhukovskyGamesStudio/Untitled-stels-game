using System.IO;
using Stealth.Configs;
using Stealth.Enemies;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string ConfigDir = DataDir + "/Config";
        const string GameConfigPath = Root + "/Resources/" + GameConfig.ResourcePath + ".asset";

        /// <summary>
        /// Tuning assets with the defaults from code. Created once and then edited by hand: never overwritten,
        /// not even by "Rebuild All". The root lives in Resources so that every build finds it.
        /// </summary>
        static void BuildConfigs()
        {
            var root = TuningAsset<GameConfig>(GameConfigPath);
            bool dirty = false;
            if (root.Player == null) { root.Player = TuningAsset<PlayerConfig>(ConfigDir + "/Player.asset"); dirty = true; }
            if (root.Run == null) { root.Run = TuningAsset<RunConfig>(ConfigDir + "/Run.asset"); dirty = true; }
            if (root.Level == null) { root.Level = TuningAsset<LevelConfig>(ConfigDir + "/Level.asset"); dirty = true; }
            if (root.Items == null) { root.Items = TuningAsset<ItemsConfig>(ConfigDir + "/Items.asset"); dirty = true; }
            if (dirty) EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            Tuning.Use(root); // item colours for the materials come from the config
        }

        /// <summary>Loads a tuning asset or creates it with the defaults from code.</summary>
        static T TuningAsset<T>(string path) where T : ScriptableObject => TuningAsset<T>(path, out _);

        static T TuningAsset<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created) return asset;
            EnsureFolder(Path.GetDirectoryName(path)!.Replace(Path.DirectorySeparatorChar, '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// Tuning of a robot / device: <c>&lt;PrefabName&gt;Config.asset</c> next to its prefab. A separate file, so that
        /// changing the numbers and changing the prefab never conflict. <paramref name="initial"/> runs only on creation.
        /// </summary>
        static T Profile<T>(string prefabName, System.Action<T> initial = null) where T : ScriptableObject
        {
            var folder = prefabName.StartsWith("Robot") ? RobotPrefabs : SensorPrefabs;
            var profile = TuningAsset<T>($"{folder}/{prefabName}Config.asset", out bool created);
            if (!created) return profile;
            initial?.Invoke(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
