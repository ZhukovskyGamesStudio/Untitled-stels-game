using Stealth.Items;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The argument a cheat of the F4 panel travels with (pure, unit tested): one int for every cheat — on/off, an item's
    /// shape and colour, an upgrade and its level — and where a spawned item appears (in front of the player).
    /// </summary>
    public static class CheatArgs
    {
        /// <summary>How far in front of the player a spawned item appears, metres (a wall nearer: just before it).</summary>
        public const float Ahead = 1.2f;
        /// <summary>The height above the player's feet it appears at (chest high: it falls to the floor).</summary>
        public const float Height = 1.1f;
        /// <summary>Kept off a wall it would appear in, metres.</summary>
        public const float WallGap = 0.35f;

        public static int Item(int shape, int color) => (shape & 0xFF) | (color & 0xFF) << 8;

        public static (byte shape, byte color) ItemOf(int arg) => ((byte)(arg & 0xFF), (byte)(arg >> 8 & 0xFF));

        public static int Upgrade(CarUpgrade upgrade, int level) => (int)upgrade & 0xFF | (level & 0xFF) << 8;

        public static (CarUpgrade upgrade, int level) UpgradeOf(int arg) => ((CarUpgrade)(arg & 0xFF), arg >> 8 & 0xFF);

        /// <summary>An item the server may spawn: a real shape and, but for a blueprint (its byte is a recipe), a real colour.</summary>
        public static bool ValidItem(int shape, int color) =>
            shape >= 0 && shape < ItemCatalog.Shapes.Length && color >= 0 && (shape == ItemCatalog.Blueprint || color < ItemCatalog.Colors.Length);

        /// <summary>
        /// Where a spawned item appears for a player at <paramref name="feet"/> facing <paramref name="yaw"/>: <see cref="Ahead"/>
        /// in front at chest height, or just before a wall <paramref name="wall"/> metres ahead (negative: none).
        /// </summary>
        public static Vector3 InFront(Vector3 feet, float yaw, float wall = -1f)
        {
            float ahead = wall >= 0f ? Mathf.Clamp(wall - WallGap, 0.2f, Ahead) : Ahead;
            return feet + Vector3.up * Height + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * ahead;
        }
    }
}
