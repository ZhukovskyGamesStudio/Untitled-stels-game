using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The lift's state (the server's, replicated as it is): the floor the cab left, the floor it goes to and the server time
    /// the trip began. Every peer works out the cab and the doors from it (<see cref="LiftRules"/>): nothing streams while it rides.
    /// </summary>
    public struct LiftState
    {
        public int From, To;
        public double Start;

        public static LiftState ParkedAt(int floor) => new() { From = floor, To = floor, Start = double.NegativeInfinity };
    }

    /// <summary>What the lift is doing: its doors closing on the floor it leaves, riding, opening on the floor it reached, standing open.</summary>
    public enum LiftPhase : byte { Closing, Riding, Opening, Open }

    /// <summary>
    /// The apartment block's lift (pure): a trip is the doors closing on the floor the cab stands at, the ride (a smooth
    /// start and stop, <paramref name="floorTime"/> per floor), the doors opening on the new floor — and there they stay
    /// open until the lift is called or sent elsewhere. A call or a floor button counts only while the lift stands open
    /// (one trip at a time); the floor it already stands at changes nothing. Times in seconds of the server's clock.
    /// </summary>
    public static class LiftRules
    {
        static float Ride(LiftState s, float floorTime) => Mathf.Abs(s.To - s.From) * floorTime;

        public static LiftPhase Phase(LiftState s, double now, float floorTime, float doorTime)
        {
            if (s.From == s.To) return LiftPhase.Open;
            double t = now - s.Start;
            if (t < doorTime) return LiftPhase.Closing;
            if (t < doorTime + Ride(s, floorTime)) return LiftPhase.Riding;
            if (t < 2f * doorTime + Ride(s, floorTime)) return LiftPhase.Opening;
            return LiftPhase.Open;
        }

        /// <summary>Where the cab is, in floors (2.5 = halfway between the second and the third).</summary>
        public static float CabFloor(LiftState s, double now, float floorTime, float doorTime)
        {
            if (s.From == s.To) return s.From;
            float ride = Ride(s, floorTime);
            float k = ride <= 0f ? 1f : Mathf.Clamp01((float)((now - s.Start - doorTime) / ride));
            return Mathf.Lerp(s.From, s.To, Mathf.SmoothStep(0f, 1f, k));
        }

        /// <summary>The floor the cab stands at, or −1 while it rides (between its doors closing and opening).</summary>
        public static int StandsAt(LiftState s, double now, float floorTime, float doorTime) => Phase(s, now, floorTime, doorTime) switch
        {
            LiftPhase.Closing => s.From,
            LiftPhase.Riding => -1,
            _ => s.To,
        };

        /// <summary>How open the doors of <paramref name="floor"/> are: 0 shut .. 1 open (only where the cab stands).</summary>
        public static float DoorOpen(LiftState s, int floor, double now, float floorTime, float doorTime)
        {
            var phase = Phase(s, now, floorTime, doorTime);
            double t = now - s.Start;
            switch (phase)
            {
                case LiftPhase.Closing: return floor == s.From ? 1f - Mathf.Clamp01((float)(t / doorTime)) : 0f;
                case LiftPhase.Riding: return 0f;
                case LiftPhase.Opening: return floor == s.To ? Mathf.Clamp01((float)((t - doorTime - Ride(s, floorTime)) / doorTime)) : 0f;
                default: return floor == s.To ? 1f : 0f;
            }
        }

        /// <summary>
        /// A call from a landing or a floor button in the cab: a new trip to <paramref name="floor"/> starting now, or the
        /// state unchanged (riding, or already there).
        /// </summary>
        public static LiftState Request(LiftState s, int floor, double now, float floorTime, float doorTime)
        {
            if (Phase(s, now, floorTime, doorTime) != LiftPhase.Open || floor == s.To) return s;
            return new LiftState { From = s.To, To = floor, Start = now };
        }

        /// <summary>
        /// Someone stands in the doorway while the doors close: they open again and the trip waits (a real lift's sensor) —
        /// nobody is left half in the cab when it moves off.
        /// </summary>
        public static LiftState Hold(LiftState s, double now, float floorTime, float doorTime)
        {
            if (Phase(s, now, floorTime, doorTime) != LiftPhase.Closing) return s;
            return new LiftState { From = s.From, To = s.To, Start = now };
        }

        /// <summary>The cab's vertical move between two moments (what a rider is carried by), metres.</summary>
        public static float Carry(LiftState s, double from, double to, float floorTime, float doorTime) =>
            (CabFloor(s, to, floorTime, doorTime) - CabFloor(s, from, floorTime, doorTime)) * LevelMetrics.FloorHeight;
    }
}
