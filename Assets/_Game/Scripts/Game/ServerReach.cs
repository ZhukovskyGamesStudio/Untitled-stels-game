using UnityEngine;

namespace Stealth
{
    /// <summary>
    /// The server's check of a player's request to use something by hand: the player's position it knows lags behind
    /// the owner's (their movement is owner-authoritative), so it allows <see cref="Slack"/> more than the reach the
    /// owner was held to. One rule for every request (pick up, open a safe, hack a door, use a terminal…).
    /// </summary>
    public static class ServerReach
    {
        /// <summary>Metres the server allows over the owner's own reach.</summary>
        public const float Slack = 1.5f;

        /// <summary>The thing at <paramref name="thing"/> is in <paramref name="reach"/> of a player at <paramref name="player"/>, as the server sees it.</summary>
        public static bool Within(Vector3 player, Vector3 thing, float reach) => (thing - player).sqrMagnitude <= Sq(reach + Slack);

        /// <summary>The same, measured flat (a thing on a wall or a shelf above the floor the player stands on).</summary>
        public static bool WithinFlat(Vector3 player, Vector3 thing, float reach)
        {
            var d = thing - player;
            d.y = 0f;
            return d.sqrMagnitude <= Sq(reach + Slack);
        }

        static float Sq(float x) => x * x;
    }
}
