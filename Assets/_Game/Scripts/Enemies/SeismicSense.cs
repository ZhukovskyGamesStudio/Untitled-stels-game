using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Floor vibration rules (pure, unit tested).</summary>
    public static class SeismicSense
    {
        /// <summary>Slower than this (crouch-walk is 1.8, but crouching is silent anyway) does not shake the floor.</summary>
        public const float MinSpeed = 1.2f;

        public static bool Feels(Vector3 sensor, Vector3 player, float playerSpeed, bool crouching, float range)
        {
            if (crouching || playerSpeed < MinSpeed) return false;
            if (!Perception.SameFloor(sensor, player)) return false;
            var d = player - sensor;
            d.y = 0;
            return d.magnitude <= range;
        }
    }
}
