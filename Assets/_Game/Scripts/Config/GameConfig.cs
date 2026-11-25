using Stealth.Items;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// Root of the tuning: points to the configs the game reads (Resources/GameConfig.asset, so it is found in every
    /// build without scene references). Swap a config here to try another balance.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        public PlayerConfig Player;
        public RunConfig Run;
        public LevelConfig Level;
        public ItemsConfig Items;
        public AudioConfig Audio;
        public PerceptionConfig Perception;
        public StyleConfig Styles;
        [Tooltip("The level settings of the test rooms houses (HouseKind.TestRooms, started from the cheat panel F4): a quiet house of " +
                 "one floor with every room type, workers only (Data/Config/RoomsTest/Level.asset). The rest of the tuning is this root's.")]
        public LevelConfig TestRoomsLevel;
    }

    /// <summary>
    /// Read access to the tuning: <c>Tuning.Player.Movement.WalkSpeed</c>. Loads Resources/GameConfig once; a missing
    /// asset or entry falls back to the defaults in code (tests, a fresh checkout before the assets are built).
    /// </summary>
    public static class Tuning
    {
        static GameConfig s_Root;
        static bool s_Loaded;
        static PlayerConfig s_Player;
        static RunConfig s_Run;
        static LevelConfig s_Level;
        static ItemsConfig s_Items;
        static AudioConfig s_Audio;
        static PerceptionConfig s_Perception;
        static StyleConfig s_Styles;

        public static PlayerConfig Player => Pick(Root != null ? Root.Player : null, ref s_Player);
        public static RunConfig Run => Pick(Root != null ? Root.Run : null, ref s_Run);
        public static LevelConfig Level => Pick(Root != null ? Root.Level : null, ref s_Level);
        public static ItemsConfig Items => Pick(Root != null ? Root.Items : null, ref s_Items);
        public static AudioConfig Audio => Pick(Root != null ? Root.Audio : null, ref s_Audio);
        public static PerceptionConfig Perception => Pick(Root != null ? Root.Perception : null, ref s_Perception);
        /// <summary>Visual styles (an empty list without the asset: the game keeps its base look).</summary>
        public static StyleConfig Styles => Pick(Root != null ? Root.Styles : null, ref s_Styles);

        /// <summary>
        /// The level settings a house of <paramref name="kind"/> is built and filled by: the test rooms' own
        /// (<see cref="GameConfig.TestRoomsLevel"/>), every other kind the game's <see cref="Level"/>.
        /// </summary>
        public static LevelConfig LevelOf(Stealth.Level.HouseKind kind) =>
            kind == Stealth.Level.HouseKind.TestRooms && Root != null && Root.TestRoomsLevel != null ? Root.TestRoomsLevel : Level;

        static GameConfig Root
        {
            get
            {
                if (s_Loaded) return s_Root;
                s_Loaded = true;
                s_Root = Resources.Load<GameConfig>(GameConfig.ResourcePath);
                if (s_Root == null) Debug.LogWarning("[Tuning] Resources/GameConfig not found — using defaults from code");
                return s_Root;
            }
        }

        static T Pick<T>(T configured, ref T fallback) where T : ScriptableObject
        {
            if (configured != null) return configured;
            if (fallback == null) fallback = ScriptableObject.CreateInstance<T>();
            return fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnGameStart() => Apply();

        /// <summary>Applies the global parts of the tuning (item colours and values). Called before the scene loads.</summary>
        public static void Apply()
        {
            ItemCatalog.Configure(Items.Colors, Items.PointsPerCell);
            ItemCatalog.DiamondValueCells = Items.Gadgets.DiamondValueCells;
            ItemCatalog.ConfigureLoot(Level.Loot.Table);
        }

        /// <summary>Use another root (tests, A/B balance); null returns to Resources/GameConfig.</summary>
        public static void Use(GameConfig root)
        {
            s_Root = root;
            s_Loaded = root != null;
            Apply();
        }
    }
}
