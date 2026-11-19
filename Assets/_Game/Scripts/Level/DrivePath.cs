using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The drive from the sewer to the next house's basement (pure, unit tested), one unbroken run of the real car with
    /// the team in its seats. It leaves the tunnel it was chosen in along the tunnel's middle; past the tunnel's 110 m
    /// a reducer opens into the kit's wide tunnel, which runs on as far as the car goes (<see cref="DriveTunnel"/>: whole
    /// segments with a lamp on alternate walls, one pattern every <see cref="Pattern"/> metres). The next house's basement
    /// has the same tunnel behind it (its approach). Deep in both, where all round there is nothing but the pattern, the
    /// car changes from the sewer's tunnel to the basement's at the same place of the pattern (<see cref="SwapLead"/>
    /// seconds before it stops): the two places look the same, so nobody — in the cab or beside it — sees a cut. Then it
    /// brakes to its stop beside the alcove (<see cref="Basement.CarStop"/>).
    /// Every peer moves the car by this from the server's clock: the departure (where the car stood, how fast it went),
    /// the arrival (<see cref="ArriveAtFor"/>) and the time.
    /// Sewer side: the branch tunnel's frame (x along it from its mouth, z across); basement side: the basement's frame.
    /// </summary>
    public static class DrivePath
    {
        /// <summary>The cruising speed (m/s), the seconds it takes to reach it, and the seconds of braking to the stop.</summary>
        public const float Cruise = 10f, SpeedUp = 3f, Brake = 4f;
        /// <summary>Seconds the car takes to come to the tunnel's lane from where it stood.</summary>
        public const float Settle = 2f;
        /// <summary>The way on in a branch: its reducer stands at this x (the branch is 110 m), the kit's tunnel starts after it.</summary>
        public const float ReducerX = 110f, TunnelFrom = ReducerX + 0.5f;
        /// <summary>The pattern of the endless tunnel: two segments, a lamp on the left wall over the first, on the right over the second.</summary>
        public const float Pattern = 2f * SewerPlan.SegmentLength;
        /// <summary>Where the car changes tunnels, basement x (in its approach), and how far past the reducer it must be then (the haze hides the rest).</summary>
        public const float SwapX = -45f, ClearOfBranch = 48f;

        public static float BrakeDistance => Cruise * Brake / 2f;

        /// <summary>The lane both tunnels drive in (across: the basement's stop).</summary>
        public static float Lane => Basement.CarStop.z;

        /// <summary>Speed <paramref name="t"/> seconds after leaving at <paramref name="v0"/>: up to cruising evenly.</summary>
        public static float SpeedAt(float t, float v0)
        {
            v0 = Mathf.Clamp(v0, 0f, Cruise);
            return t >= SpeedUp ? Cruise : Mathf.Lerp(v0, Cruise, Mathf.Max(0f, t) / SpeedUp);
        }

        /// <summary>Metres driven <paramref name="t"/> seconds after leaving at <paramref name="v0"/>.</summary>
        public static float Travel(float t, float v0)
        {
            v0 = Mathf.Clamp(v0, 0f, Cruise);
            t = Mathf.Max(0f, t);
            float a = (Cruise - v0) / SpeedUp;
            if (t <= SpeedUp) return v0 * t + a * t * t / 2f;
            return v0 * SpeedUp + a * SpeedUp * SpeedUp / 2f + Cruise * (t - SpeedUp);
        }

        /// <summary>Metres to the stop <paramref name="left"/> seconds before it (cruising, then braking evenly), and the speed then.</summary>
        public static float ToGo(float left, out float speed)
        {
            left = Mathf.Max(0f, left);
            if (left <= Brake)
            {
                speed = Cruise * left / Brake;
                return Cruise * left * left / (2f * Brake);
            }
            speed = Cruise;
            return BrakeDistance + Cruise * (left - Brake);
        }

        /// <summary>Seconds before the stop the car changes tunnels (it is at <see cref="SwapX"/> then, cruising).</summary>
        public static float SwapLead => Brake + (Basement.CarStop.x - SwapX - BrakeDistance) / Cruise;

        /// <summary>
        /// The car in the branch <paramref name="t"/> seconds after it left from <paramref name="from"/> (branch frame: x, the
        /// heading in radians from +X towards +Z, z) at <paramref name="v0"/>: along the tunnel, easing into its lane.
        /// </summary>
        public static Pose InBranch(Vector3 from, float v0, float t, out float speed)
        {
            speed = SpeedAt(t, v0);
            float x = from.x + Travel(t, v0);
            float k = Mathf.Clamp01(t / Settle);
            float ease = k * k * (3f - 2f * k), slope = 6f * k * (1f - k) / Settle; // smoothstep and its time derivative
            float z = Mathf.Lerp(from.z, Lane, ease);
            float heading = Mathf.Atan2((Lane - from.z) * slope, Mathf.Max(0.5f, speed));
            float turn = Mathf.Clamp01(t / 0.6f);
            heading = Mathf.LerpAngle(from.y * Mathf.Rad2Deg, heading * Mathf.Rad2Deg, turn * turn * (3f - 2f * turn)) * Mathf.Deg2Rad;
            return new Pose(new Vector3(x, Basement.Road, z), Quaternion.Euler(0f, 90f - heading * Mathf.Rad2Deg, 0f));
        }

        /// <summary>The car in the basement's frame <paramref name="left"/> seconds before it stops: down the approach to the stop.</summary>
        public static Pose InBasement(float left, out float speed)
        {
            float behind = ToGo(left, out speed);
            return new Pose(new Vector3(Basement.CarStop.x - behind, Basement.Road, Lane), Quaternion.Euler(0f, 90f, 0f));
        }

        /// <summary>
        /// The server's arrival time: no sooner than <paramref name="earliest"/>, late enough that the car has left the branch
        /// well behind when it changes tunnels, and at the moment that puts it at the same place of the pattern in both.
        /// </summary>
        public static double ArriveAtFor(double departed, Vector3 from, float v0, double earliest)
        {
            double arrive = System.Math.Max(earliest, departed + SpeedUp + SwapLead);
            // past the reducer by enough when it changes tunnels
            float x = BranchXAt(departed, from, v0, arrive);
            if (x < TunnelFrom + ClearOfBranch) arrive += (TunnelFrom + ClearOfBranch - x) / Cruise;
            // the same place of the pattern: (branch x − where the tunnel starts) ≡ basement x (mod the pattern)
            float off = BranchXAt(departed, from, v0, arrive) - TunnelFrom - SwapX;
            float wrap = Mathf.Repeat(-off, Pattern);
            return arrive + wrap / Cruise;
        }

        /// <summary>Where the car is along the branch when it changes tunnels, for this arrival.</summary>
        public static float BranchXAt(double departed, Vector3 from, float v0, double arrive) =>
            from.x + Travel((float)(arrive - SwapLead - departed), v0);

        /// <summary>Of the endless tunnel's pattern: the segment variant (of two) and the wall of its lamp (+1 left, −1 right), for segment k.</summary>
        public static int VariantOf(int k) => ((k % 2) + 2) % 2;
        public static int LampSideOf(int k) => VariantOf(k) == 0 ? 1 : -1;
    }
}
