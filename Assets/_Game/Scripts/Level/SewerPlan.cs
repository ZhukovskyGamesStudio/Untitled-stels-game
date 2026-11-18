using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>One straight piece of the sewer tunnel (a 6 m segment of one of the kit's variants) and what stands in it.</summary>
    public struct SewerSegment
    {
        public int Variant;
        /// <summary>Where it starts along the tunnel (+X), metres.</summary>
        public float X;
        /// <summary>A lamp on the wall (+1 the left wall, −1 the right, 0 none) and whether it is broken (flickers).</summary>
        public int LampSide;
        public bool LampBroken;
        /// <summary>The kit's segment with the alcove (its shaft up to the house the team came in through, the lid shut) instead of a variant.</summary>
        public bool Alcove;
        /// <summary>Junk on the walkways: barrels and crates (x along, side ±1).</summary>
        public List<(float x, int side, bool barrel)> Junk;
    }

    /// <summary>
    /// The sewer between two houses, from the run's seed and the depth (pure, unit tested): a closed end, the stretch
    /// where the car waits under the hatch the team came in through, a gate, the stretch the house's exit drops into,
    /// and the hall where three tunnels lead on. Lengths, segment variants, lamps, their colour and junk differ every
    /// time, so every stretch feels like another part of the sewer. Along Unity +X; the walkways at z = ±3.1.
    /// </summary>
    public sealed class SewerPlan
    {
        public const float SegmentLength = 6f;
        public const int Variants = 4;
        public const float HubLength = 16f;
        /// <summary>The kit's hall: the tunnels leave its far end at these z (left, middle, right when driving +X).</summary>
        public static readonly float[] BranchZ = { 5.5f, 0f, -5.5f };

        /// <summary>
        /// The hall's five mouths (Tools/Blender/sewer.py kit_hub), hall-local: three in its end wall, one in each long
        /// wall; a branch tunnel (or a brick plug) at a mouth runs along the pose's +X.
        /// </summary>
        public static readonly Pose[] Mouths =
        {
            new(new Vector3(HubLength, 0f, 5.5f), Quaternion.identity),
            new(new Vector3(HubLength, 0f, 0f), Quaternion.identity),
            new(new Vector3(HubLength, 0f, -5.5f), Quaternion.identity),
            new(new Vector3(10f, 0f, 8.6f), Quaternion.Euler(0f, -90f, 0f)),
            new(new Vector3(10f, 0f, -8.6f), Quaternion.Euler(0f, 90f, 0f)),
        };

        /// <summary>Which mouth each of the three tunnels (SewerBranches) leaves the hall by; the other two are bricked up.</summary>
        public readonly int[] TunnelMouths = { 0, 1, 2 };
        /// <summary>Walkways: z of their middle (left, right).</summary>
        public const float WalkwayZ = 3.1f;

        public readonly List<SewerSegment> Segments = new();
        /// <summary>The gate between the car and the exit's stretch (x), or −1: no gate (the run's first sewer).</summary>
        public float GateX = -1f;
        /// <summary>Where the car waits (x, on the road, facing +X) and the middle of the alcove beside it (the shaft up to the house the team came in through).</summary>
        public float CarX, EntryLadderX = -1f;
        /// <summary>The ladder from the house's exit (x; on the left walkway), −1 = none: the team starts at the car.</summary>
        public float ExitLadderX = -1f;
        public float HubX;
        /// <summary>The lamps' colour this time.</summary>
        public Color LampColor;
        public float LampIntensity;

        static readonly Color[] Palette =
        {
            new(1f, 0.78f, 0.5f),   // old sodium
            new(0.75f, 0.9f, 1f),   // cold strip lights
            new(0.75f, 1f, 0.7f),   // sickly green
            new(1f, 0.62f, 0.35f),  // amber
        };

        public float End => HubX + HubLength;

        /// <summary>The sewer after house <paramref name="depth"/> (−1 = the start of the run: no gate, no exit ladder).</summary>
        public static SewerPlan Make(int runSeed, int depth)
        {
            var rng = new System.Random(LevelRandom.Mix(runSeed, 0x5E3E, depth + 7));
            var p = new SewerPlan
            {
                LampColor = Palette[rng.Next(Palette.Length)],
                LampIntensity = 7f + (float)rng.NextDouble() * 3f,
            };
            bool afterHouse = depth >= 0;
            int behind = 2 + rng.Next(2);           // the car's stretch
            int ahead = afterHouse ? 2 + rng.Next(2) : 1; // the exit's stretch
            int last = -1;
            float x = 0f;
            void Add()
            {
                int v = rng.Next(Variants);
                if (v == last) v = (v + 1) % Variants;
                last = v;
                var s = new SewerSegment
                {
                    Variant = v, X = x,
                    LampSide = rng.NextDouble() < 0.92 ? (rng.Next(2) == 0 ? 1 : -1) : 0,
                    LampBroken = rng.NextDouble() < 0.2,
                    Junk = new List<(float, int, bool)>(),
                };
                int junk = rng.Next(3);
                for (int j = 0; j < junk; j++) s.Junk.Add(((float)(x + 0.8 + rng.NextDouble() * 4.4), rng.Next(2) == 0 ? 1 : -1, rng.Next(2) == 0));
                p.Segments.Add(s);
                x += SegmentLength;
            }
            for (int i = 0; i < behind; i++) Add();
            if (afterHouse)
            {
                // the car stopped where it stopped in the house's basement (Basement): beside the alcove whose ladder the team
                // climbed — its lid is shut now, there is no way back up
                var alcove = p.Segments[behind - 1];
                alcove.Alcove = true;
                alcove.LampSide = -1;
                alcove.Junk.RemoveAll(j => j.side > 0);
                p.Segments[behind - 1] = alcove;
            }
            p.EntryLadderX = afterHouse ? SegmentLength * (behind - 1) + 3f : -1f;
            p.CarX = SegmentLength * behind - 4.2f; // its nose a little before the gate
            if (afterHouse) p.GateX = x;
            for (int i = 0; i < ahead; i++) Add();
            p.ExitLadderX = afterHouse ? p.GateX + SegmentLength * 0.5f + (float)rng.NextDouble() * SegmentLength * (ahead - 1) : -1f;
            p.HubX = x;
            // the ways on leave the hall wherever this stretch of sewer has them: ahead, to the sides, or both
            var mouths = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = mouths.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (mouths[i], mouths[j]) = (mouths[j], mouths[i]); }
            for (int i = 0; i < 3; i++) p.TunnelMouths[i] = mouths[i];
            return p;
        }
    }
}
