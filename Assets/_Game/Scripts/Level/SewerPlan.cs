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
        /// <summary>The kit's segment with a doorway in its wall instead of a variant: +1 the left wall (+Z), −1 the right, 0 none (the way to the exit hatch).</summary>
        public int Door;
    }

    /// <summary>A piece of the way from the sewer to the shaft under the house's exit hatch (sewer-local, on the walkway's level).</summary>
    public struct PassagePiece
    {
        public PassageKind Kind;
        /// <summary>Straight and End: where the piece starts (its open end); Corner: its middle.</summary>
        public Vector3 Position;
        /// <summary>The turn about the vertical (degrees) that takes the piece's own +X (and, for a corner, its +Z opening) where it goes.</summary>
        public float Yaw;
        /// <summary>Straight: its length (the game lays it with the kit's 1 m pieces, the last stretched to fit).</summary>
        public float Length;
    }

    /// <summary>The way's pieces: a stretch of passage, a turn, the shaft's foot under the hatch.</summary>
    public enum PassageKind : byte { Straight, Corner, End }

    /// <summary>
    /// The sewer between two houses, from the run's seed and the depth (pure, unit tested): a closed end, the stretch
    /// where the car waits under the hatch the team came in through, a gate, the stretch the house's exit drops into,
    /// and the hall where three tunnels lead on. Lengths, segment variants, lamps, their colour and junk differ every
    /// time, so every stretch feels like another part of the sewer, and so does the hall's shape (<see cref="SewerHall"/>).
    /// Along Unity +X; the walkways at z = ±3.1.
    /// </summary>
    public sealed class SewerPlan
    {
        public const float SegmentLength = 6f;
        public const int Variants = 4;
        /// <summary>
        /// The hall where the ways on leave (<see cref="SewerHall"/>: its shape, the mouths, the tunnels' mouths),
        /// hall-local from <see cref="HubX"/>; a branch tunnel (or a brick plug) at a mouth runs along the pose's +X.
        /// </summary>
        public SewerHall Hall;
        /// <summary>The hall's mouths, hall-local (+X leads into the tunnel or the plug).</summary>
        public Pose[] Mouths => Hall.Mouths;
        /// <summary>Which mouth each of the three tunnels (SewerBranches: left, middle, right) leaves the hall by; the others are bricked up.</summary>
        public int[] TunnelMouths => Hall.Tunnels;
        /// <summary>Walkways: z of their middle (left, right).</summary>
        public const float WalkwayZ = 3.1f;

        public readonly List<SewerSegment> Segments = new();

        // ------------------------------------------------------------------ the way to the house's exit hatch

        /// <summary>Half the passage's width (the shaft's hole is 3 × 3 plan cells), where the doorway's lining ends across the tunnel.</summary>
        public const float PassageHalf = 0.75f, DoorOut = 4.2f;
        /// <summary>
        /// The middle of the passage's stretch alongside the tunnel: right behind the tunnel's wall, or further out where it
        /// passes the basement's alcove and its shaft (on the left, +Z, by <see cref="Basement.AlcoveX"/>).
        /// </summary>
        public const float AlongZ = DoorOut + PassageHalf, AlongPastAlcove = 6.25f;
        /// <summary>
        /// Where the house's exit hatch may stand for the passage to reach it (in the basement's frame, <see cref="Basement"/>):
        /// its shaft clear of the tunnel and of the alcove (the last stretch may run along), anywhere along it — behind the
        /// basement's invisible wall too (the wall stands across the tunnel only).
        /// </summary>
        public const float HatchMinZ = Basement.AlcoveBack + 0.1f + PassageHalf;
        /// <summary>The hall begins at least this far past the exit hatch (along the tunnel): the passage never runs under it.</summary>
        public const float HallPastHatch = 4f;

        /// <summary>The way from a doorway in the exit's stretch to the shaft under the house's exit hatch (empty: no house above, or no way).</summary>
        public readonly List<PassagePiece> Passage = new();
        /// <summary>The exit hatch over the passage's end (sewer-local x, z; the house's floor is <see cref="Basement.Depth"/> over the road).</summary>
        public Vector2 ExitHatch;

        /// <summary>The house's exit hatch at <paramref name="hatch"/> (the basement's frame: x along the tunnel, z across) can be reached by a passage.</summary>
        public static bool PassageFits(Vector2 hatch) => Mathf.Abs(hatch.y) >= HatchMinZ;
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

        /// <summary>
        /// The passage to the hatch at (<paramref name="hx"/>, <paramref name="hz"/>): a doorway in one of the exit's stretch's
        /// segments (not the first, by the gate's lever) on the hatch's side, out of the tunnel across it; if the hatch is not
        /// straight across, a turn, alongside the tunnel (clear of the basement's alcove), a turn back, and across again to
        /// the shaft's foot right under the hatch.
        /// </summary>
        void LayPassage(float hx, float hz, int behind)
        {
            int side = hz >= 0f ? 1 : -1;
            int best = -1;
            float bestOff = float.MaxValue;
            for (int i = behind + 1; i < Segments.Count; i++)
            {
                float off = Segments[i].X + SegmentLength / 2f - hx;
                if (Mathf.Abs(off) > 0.01f && Mathf.Abs(off) < 2f * PassageHalf) continue; // too little room for the two turns
                if (Mathf.Abs(off) < bestOff) { bestOff = Mathf.Abs(off); best = i; }
            }
            if (best < 0) return;
            var seg = Segments[best];
            seg.Door = side;
            seg.LampSide = -side; // its lamp on the other wall
            seg.Junk.RemoveAll(j => j.side == side);
            Segments[best] = seg;
            float dx = seg.X + SegmentLength / 2f, far = Mathf.Abs(hz) - PassageHalf;
            var across = new Vector3(0f, 0f, side);
            ExitHatch = new Vector2(hx, hz);
            if (Mathf.Abs(dx - hx) < 0.01f)
            {
                // straight across to the shaft's foot
                Straight(new Vector3(dx, 0f, side * DoorOut), across, far - DoorOut);
                Passage.Add(new PassagePiece { Kind = PassageKind.End, Position = new Vector3(hx, 0f, side * far), Yaw = YawOf(across) });
                return;
            }
            var along = new Vector3(Mathf.Sign(hx - dx), 0f, 0f);
            // the alcove (sewer-local: the basement's x less the shift of the frames) on the left, where the way along passes it
            float shift = Basement.GateX - SegmentLength * behind;
            float a0 = Basement.AlcoveX - Basement.AlcoveHalf - 0.2f - shift, a1 = Basement.AlcoveX + Basement.AlcoveHalf + 0.2f - shift;
            bool byAlcove = side > 0 && Mathf.Min(dx, hx) - PassageHalf < a1 && Mathf.Max(dx, hx) + PassageHalf > a0;
            float turnZ = byAlcove ? AlongPastAlcove : AlongZ;
            if (Mathf.Abs(hz) >= turnZ + 2f * PassageHalf)
            {
                // across, a turn, along behind the tunnel's wall, a turn back, across to the shaft's foot
                Straight(new Vector3(dx, 0f, side * DoorOut), across, turnZ - PassageHalf - DoorOut);
                Corner(new Vector3(dx, 0f, side * turnZ), across, along);
                Straight(new Vector3(dx, 0f, side * turnZ) + along * PassageHalf, along, Mathf.Abs(hx - dx) - 2f * PassageHalf);
                Corner(new Vector3(hx, 0f, side * turnZ), along, across);
                Straight(new Vector3(hx, 0f, side * (turnZ + PassageHalf)), across, far - turnZ - PassageHalf);
                Passage.Add(new PassagePiece { Kind = PassageKind.End, Position = new Vector3(hx, 0f, side * far), Yaw = YawOf(across) });
                return;
            }
            // the hatch near the tunnel: across to its line, a turn, along it to the shaft's foot
            Straight(new Vector3(dx, 0f, side * DoorOut), across, far - DoorOut);
            Corner(new Vector3(dx, 0f, hz), across, along);
            Straight(new Vector3(dx, 0f, hz) + along * PassageHalf, along, Mathf.Abs(hx - dx) - 2f * PassageHalf);
            Passage.Add(new PassagePiece { Kind = PassageKind.End, Position = new Vector3(hx, 0f, hz) - along * PassageHalf, Yaw = YawOf(along) });
        }

        void Straight(Vector3 from, Vector3 dir, float length)
        {
            if (length > 0.01f) Passage.Add(new PassagePiece { Kind = PassageKind.Straight, Position = from, Yaw = YawOf(dir), Length = length });
        }

        /// <summary>A turn from going <paramref name="from"/> to going <paramref name="to"/>: its openings (own −X and +Z) face back the way in and on the way out.</summary>
        void Corner(Vector3 at, Vector3 from, Vector3 to)
        {
            float yaw = 0f;
            for (int k = 0; k < 4; k++)
            {
                var r = Quaternion.Euler(0f, k * 90f, 0f);
                Vector3 a = r * Vector3.left, b = r * Vector3.forward;
                if ((Close(a, -from) && Close(b, to)) || (Close(a, to) && Close(b, -from))) { yaw = k * 90f; break; }
            }
            Passage.Add(new PassagePiece { Kind = PassageKind.Corner, Position = at, Yaw = yaw });
        }

        static bool Close(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-3f;

        /// <summary>The turn that takes a piece's own +X along <paramref name="dir"/> (flat).</summary>
        public static float YawOf(Vector3 dir) => Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

        /// <summary>The far end of the hall (its farthest wall, outer face), along +X.</summary>
        public float End => HubX + Hall.MaxX;

        /// <summary>
        /// The sewer after house <paramref name="depth"/> (−1 = the start of the run: no gate, no exit ladder). Under the house
        /// (<paramref name="exitHatch"/>: the house's exit hatch in the basement's frame, x along the tunnel, z across) the
        /// exit's stretch runs on past the hatch and a passage leads from a doorway in it to the shaft under the hatch: the
        /// team climbs down and up there (<see cref="Passage"/>); no ladder in the tunnel itself then.
        /// </summary>
        public static SewerPlan Make(int runSeed, int depth, Vector2? exitHatch = null)
        {
            var rng = new System.Random(LevelRandom.Mix(runSeed, Salt.Sewer, depth + 7));
            var p = new SewerPlan
            {
                LampColor = Palette[rng.Next(Palette.Length)],
                LampIntensity = 3.2f + (float)rng.NextDouble() * 1.4f, // dim: pools of light, the dark between them
            };
            bool afterHouse = depth >= 0;
            int behind = 2 + rng.Next(2);           // the car's stretch
            int ahead = afterHouse ? 2 + rng.Next(2) : 1; // the exit's stretch
            // under the house: the exit's hatch in this sewer's frame (its gate where the basement's stands)
            bool passage = afterHouse && exitHatch is Vector2 h && PassageFits(h);
            float hatchX = passage ? exitHatch.Value.x - (Basement.GateX - SegmentLength * behind) : 0f;
            if (passage) ahead = Mathf.Max(ahead, 3, Mathf.CeilToInt((hatchX + HallPastHatch - SegmentLength * behind) / SegmentLength));
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
            if (passage)
            {
                p.ExitLadderX = -1f; // (the way up is the passage's shaft)
                p.LayPassage(hatchX, exitHatch.Value.y, behind);
            }
            // the hall's shape and its ways on: a stream of its own (the rolls above stay where they were)
            p.Hall = SewerHall.Make(new System.Random(LevelRandom.Mix(runSeed, Salt.SewerHall, depth + 7)));
            return p;
        }
    }
}
