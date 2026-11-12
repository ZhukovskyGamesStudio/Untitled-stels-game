using Stealth.Player;

namespace Stealth.Game
{
    /// <summary>
    /// Every test / debug hook in one place. Set only by <see cref="DevAutomation"/> (command line) and tests;
    /// in normal play they all keep their defaults.
    /// </summary>
    public static class DevFlags
    {
        /// <summary>Seed for the next level (0 = random). Consumed once.</summary>
        public static int ForcedSeed;
        /// <summary>Every staircase shows this look (StairStyle index), −1 = the one its family and seed pick (autopilot "floors").</summary>
        public static int ForcedStairStyle = -1;
        /// <summary>Start the run at this level index. Consumed once.</summary>
        public static int ForcedLevel;
        /// <summary>Start the run right in the first house instead of the sewer (autopilot scenarios).</summary>
        public static bool StartInHouse;
        /// <summary>Robots and sensors ignore players (UI screenshots).</summary>
        public static bool Peaceful;
        /// <summary>Robots stand still (gallery screenshots).</summary>
        public static bool FreezeRobots;
        /// <summary>Robots sense and decide but stay where they are (measuring detection times).</summary>
        public static bool HoldRobots;
        /// <summary>No sound at all (autotests run next to a working developer; -autotest-sound turns it back on).</summary>
        public static bool Mute;
        /// <summary>Replaces keyboard / mouse input of the local player.</summary>
        public static PlayerInputState? Autopilot;
        /// <summary>Behave as if E is held.</summary>
        public static bool AutoInteract;
        /// <summary>Behave as if Tab is held (hands mode, cursor visible).</summary>
        public static bool ForceHandsMode;
        /// <summary>Sensors work from the first second of a house (robot checks).</summary>
        public static bool SkipGracePeriod;
        /// <summary>Show these friends' sessions in the main menu instead of asking Steam (menu screenshots).</summary>
        public static System.Collections.Generic.IReadOnlyList<FriendLobby> FriendLobbiesOverride;

        public static void Reset()
        {
            ForcedSeed = 0;
            ForcedLevel = 0;
            Peaceful = false;
            FreezeRobots = false;
            HoldRobots = false;
            Mute = false;
            Autopilot = null;
            AutoInteract = false;
            ForceHandsMode = false;
            SkipGracePeriod = false;
            FriendLobbiesOverride = null;
        }
    }
}
