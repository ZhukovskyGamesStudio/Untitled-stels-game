using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>Tuning of what the workbench's things do (ItemsConfig → Crafted).</summary>
    [System.Serializable]
    public class CraftedSettings
    {
        [Header("Upgraded screwdriver: once per house unscrews any device for good")]
        [Tooltip("Seconds of holding E at a device.")]
        public float UnscrewTime = 2.5f;
        [Tooltip("Things an unscrewed device drops (min, max): a circuit board, a bundle of wires, a chip.")]
        public Vector2Int UnscrewDrops = new(1, 2);

        [Header("Upgraded keyboard")]
        [Tooltip("Hack slots the team gets on top of the usual while anyone carries one.")]
        public int ExtraHackSlots = 1;

        [Header("Double espresso: running into a robot knocks it out")]
        [Tooltip("Seconds a rammed robot stays out.")]
        public float RamStun = 10f;
        [Tooltip("How close (metres between the body and the robot) a running player rams it.")]
        public float RamReach = 0.9f;

        [Header("Jump boots (while they are in the inventory)")]
        [Tooltip("How high a jump goes, metres: a balcony of the second floor (4.1 m up, its parapet 0.75 m over that) in one jump.")]
        public float JumpHeight = 5.0f;

        [Header("Gas mask (while it is in the inventory)")]
        [Tooltip("Seconds in the gas outside before passing out (without it: Player → Gas → Limit).")]
        public float GasLimit = 60f;

        [Header("Heavy hammer: a hole in the floor, once per house per person")]
        [Tooltip("The hole's side, metres (a square under the aim, on the plan's cells).")]
        public float HoleSize = 1.0f;
        [Tooltip("How far the floor under the aim may be, metres.")]
        public float HoleReach = 2.6f;
        [Tooltip("How far robots hear the blow, metres.")]
        public float HoleNoise = 18f;

        [Header("Robot motion sensor (placed on the floor): a motion sensor that marks robots")]
        [Tooltip("How far it sees a robot, metres.")]
        public float SensorRange = 9f;
        [Tooltip("Its view, degrees (around the way it faces when put down).")]
        public float SensorAngle = 100f;
        [Tooltip("A robot slower than this (m/s) is not seen.")]
        public float SensorSpeed = 0.3f;

        [Header("Upgraded fuel finder: its ring of lamps shows which way the nearest loot lies")]
        [Tooltip("How far it feels loot, metres: further, the ring is dark (the lamp in the middle still blinks slowly).")]
        public float FinderRange = 30f;
        [Tooltip("How wide the lit part of the ring is, degrees each side, with the loot close and at the edge of the range: far away it shows only the general way.")]
        public float FinderSpreadNear = 20f, FinderSpreadFar = 60f;
        [Tooltip("How bright the ring stays between the blinks (the blink itself is full).")]
        [Range(0f, 1f)] public float FinderSteady = 0.45f;
    }

    /// <summary>What the workbench's things do (pure, unit tested): the numbers the components ask for.</summary>
    public static class CraftedRules
    {
        /// <summary>The team's hack slots: the usual, more while anyone carries an upgraded keyboard.</summary>
        public static int HackSlots(int usual, bool upgraded, CraftedSettings s) => Mathf.Max(1, usual) + (upgraded ? Mathf.Max(0, s.ExtraHackSlots) : 0);

        /// <summary>Seconds in the gas outside before passing out: longer with a gas mask.</summary>
        public static float GasLimit(float usual, bool mask, CraftedSettings s) => mask ? Mathf.Max(usual, s.GasLimit) : usual;

        /// <summary>How high a jump goes: much higher with the jump boots in the inventory.</summary>
        public static float JumpHeight(float usual, bool jumpBoots, CraftedSettings s) => jumpBoots ? Mathf.Max(usual, s.JumpHeight) : usual;

        /// <summary>Whether a running player under a double espresso rams a robot this far from their body.</summary>
        public static bool Rams(bool running, bool espresso, float distance, CraftedSettings s) => running && espresso && distance <= s.RamReach;

        /// <summary>
        /// The colour byte of a thing used up in this house (by its seed; the upgraded screwdriver: once per house). It travels
        /// with the thing (a teammate who gets it gets it used); 0 is "never used" (a made thing starts with 0).
        /// </summary>
        public static byte UsedIn(int house) => (byte)((house & int.MaxValue) % 255 + 1);

        public static bool IsUsedIn(byte color, int house) => color != 0 && color == UsedIn(house);

        /// <summary>What an unscrewed device drops: one to a few of a circuit board, a bundle of wires and a chip.</summary>
        public static List<byte> UnscrewDrops(System.Random rng, CraftedSettings s)
        {
            int n = rng.Next(Mathf.Max(1, s.UnscrewDrops.x), Mathf.Max(s.UnscrewDrops.x, s.UnscrewDrops.y) + 1);
            var drops = new List<byte>(n);
            byte[] kinds = { ItemCatalog.Circuit, ItemCatalog.Wires, ItemCatalog.Chip };
            for (int i = 0; i < n; i++) drops.Add(kinds[rng.Next(kinds.Length)]);
            return drops;
        }

        /// <summary>A chess piece for the chess detector: its kind by the weights (pawn … king), white or black evenly.</summary>
        public static byte ChessPiece(System.Random rng, int[] weights)
        {
            int total = 0;
            for (int k = 0; k < 6; k++) total += weights != null && k < weights.Length ? Mathf.Max(0, weights[k]) : 1;
            int roll = rng.Next(Mathf.Max(1, total)), kind = 0;
            for (int k = 0; k < 6; k++)
            {
                int w = weights != null && k < weights.Length ? Mathf.Max(0, weights[k]) : 1;
                if (roll < w) { kind = k; break; }
                roll -= w;
            }
            return ItemCatalog.ChessPiece(kind, rng.Next(2) == 1);
        }

        /// <summary>
        /// How bright a lamp of the upgraded fuel finder's ring is (0..1): the lamp that points from the dial
        /// (<paramref name="lampWay"/>) the way the nearest loot lies (<paramref name="lootWay"/>, <paramref name="distance"/>
        /// metres off; both flattened), its neighbours dimmer — the further the loot, the wider and vaguer; dark out of range.
        /// </summary>
        public static float DialGlow(Vector3 lampWay, Vector3 lootWay, float distance, CraftedSettings s)
        {
            if (float.IsInfinity(distance) || float.IsNaN(distance) || distance > s.FinderRange) return 0f;
            var a = new Vector3(lampWay.x, 0f, lampWay.z);
            var b = new Vector3(lootWay.x, 0f, lootWay.z);
            if (b.sqrMagnitude < 0.01f) return 1f; // right under the dial: the whole ring
            if (a.sqrMagnitude < 1e-8f) return 0f;
            float spread = Mathf.Max(1f, Mathf.Lerp(s.FinderSpreadNear, s.FinderSpreadFar, Mathf.Clamp01(distance / Mathf.Max(0.01f, s.FinderRange))));
            return Mathf.Clamp01(1f - Vector3.Angle(a, b) / spread);
        }

        /// <summary>
        /// Whether a robot moving at <paramref name="speed"/> <paramref name="offset"/> from the robot sensor (facing
        /// <paramref name="facing"/>, flat) is in its sight (a clear line is checked by the component).
        /// </summary>
        public static bool SensorSees(Vector3 offset, Vector3 facing, float speed, CraftedSettings s)
        {
            if (speed <= s.SensorSpeed || offset.magnitude > s.SensorRange) return false;
            var flat = new Vector3(offset.x, 0f, offset.z);
            var f = new Vector3(facing.x, 0f, facing.z);
            if (flat.sqrMagnitude < 0.01f || f.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(f, flat) <= s.SensorAngle * 0.5f;
        }
    }
}
