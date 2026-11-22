using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>What the team's storage robot is doing (replicated, append only).</summary>
    public enum BotMode : byte { Waiting, Coming, ToGrate, InDucts, ToExit, Gone, WaitingAbove, Hiding }

    /// <summary>One stretch of the storage robot's way.</summary>
    public enum BotLegKind : byte { Walk, ClimbUp, Crawl, ClimbDown, LeaveByHatch }

    public struct BotLeg
    {
        public BotLegKind Kind;
        /// <summary>Walk: where to; ClimbUp/ClimbDown: the grate's middle on the ceiling.</summary>
        public Vector3 To;
        /// <summary>Crawl: the duct's floor, cell by cell.</summary>
        public List<Vector3> Route;
        /// <summary>ClimbDown: only if the room under the grate is safe (else it waits up there).</summary>
        public bool CheckSafe;
        /// <summary>The grate's room (ClimbDown), the floor of a walk.</summary>
        public int Room;
    }

    /// <summary>
    /// The storage robot's rules (pure, tested). It lives in the ducts: called, it crawls to the grate nearest the player
    /// and comes down only if the room under it is safe (no awake robot, camera or sensor there) — otherwise it waits up in
    /// the duct, its stash in reach of a player standing under the grate. Out of the ducts it walks only the last stretch
    /// (to the player, to the exit hatch, home to the basement). Sent to the exit (once the exit room is seen) it crawls to
    /// the exit room and leaves by the exit hatch with its load; on an alarm it hides in the basement. Robots that see it in
    /// a room raise the alarm.
    /// </summary>
    public static class StorageBotRules
    {
        /// <summary>The stash board shows and the fuel burns within this of the robot, metres.</summary>
        public const float StashReach = 2.4f;
        /// <summary>Called, it stops this close to the player who called it.</summary>
        public const float Arrive = 1.6f;
        /// <summary>The longest way it walks on the floor to a caller or the exit (metres); further, or no way at all: through the ducts.</summary>
        public const float WalkFurthest = 45f;
        /// <summary>Crawling through the ducts, metres a second; climbing through a grate, the same.</summary>
        public const float DuctSpeed = 1.8f;
        /// <summary>Seen in a room by a robot or a camera for this long, the alarm goes off, seconds.</summary>
        public const float SeenFor = 1.2f;
        /// <summary>A watcher sees it this far (and within half this angle of where it looks), metres / degrees.</summary>
        public const float SeenAt = 11f, SeenAngle = 110f;

        /// <summary>The duct's floor over a storey: where it crawls.</summary>
        public static float DuctFloor(int floor) => LevelLayout.FloorY(floor) + LevelMetrics.WallHeight + LevelMetrics.CeilingBoard + 0.02f;

        /// <summary>
        /// Its stash is in reach of a player here: next to it in a room; or, while it waits up in the duct, from right under
        /// its grate (things are passed up through it).
        /// </summary>
        public static bool InReach(BotMode mode, Vector3 bot, Vector3 player)
        {
            float flat = Flat(bot - player), up = bot.y - player.y;
            if (mode == BotMode.WaitingAbove) return flat <= 1.4f && up > 1.5f && up < 4f;
            return mode is BotMode.Waiting or BotMode.Coming && flat <= StashReach && Mathf.Abs(up) < 1.8f;
        }

        /// <summary>A player may call it now (it is in the house, not on its way out).</summary>
        public static bool CanCall(BotMode mode) => mode is not (BotMode.ToExit or BotMode.Gone);

        /// <summary>It may be sent to the exit: the team has seen the exit room, and it is not on its way already.</summary>
        public static bool CanSendToExit(BotMode mode, bool exitSeen) => exitSeen && CanCall(mode) && mode != BotMode.ToGrate;

        /// <summary>The grate of a storey's ducts nearest a point (flat distance), or null.</summary>
        public static DuctGrate? NearestGrate(VentNet net, Vector3 p)
        {
            DuctGrate? best = null;
            float d = float.MaxValue;
            foreach (var g in net.Grates)
            {
                float dd = Flat(g.Position - p);
                if (dd < d) { d = dd; best = g; }
            }
            return best;
        }

        /// <summary>The duct's floor between two grates of a storey, cell by cell (both ends over their grates), or null.</summary>
        public static List<Vector3> Crawl(VentNet net, Vector2Int from, Vector2Int to)
        {
            var cells = VentPlan.Path(net, from, to);
            if (cells == null) return null;
            float y = DuctFloor(net.Floor);
            var route = new List<Vector3>();
            foreach (var c in cells) route.Add(VentPlan.CellCenter(c, y));
            return route;
        }

        /// <summary>
        /// Its way through the ducts of a storey (cells of <see cref="VentPlan"/>): up from the floor under the first grate
        /// into the duct, along the duct's floor cell by cell, down to the floor under the last grate. Null without a duct
        /// between them.
        /// </summary>
        public static List<Vector3> DuctRoute(VentNet net, Vector2Int from, Vector2Int to)
        {
            var crawl = Crawl(net, from, to);
            if (crawl == null) return null;
            float floor = LevelLayout.FloorY(net.Floor);
            var route = new List<Vector3> { VentPlan.CellCenter(from, floor) };
            route.AddRange(crawl);
            route.Add(VentPlan.CellCenter(to, floor));
            return route;
        }

        /// <summary>
        /// A room is safe to come down into: no awake watcher in it (a robot, a camera, a sensor — <paramref name="watchers"/>
        /// lists their rooms), and no alarm going.
        /// </summary>
        public static bool Safe(int room, IEnumerable<int> watchers, bool alarm)
        {
            if (alarm) return false;
            foreach (int w in watchers) if (w == room) return false;
            return true;
        }

        /// <summary>A watcher sees it: close enough, within its view's angle, nothing between (pure: the caller casts the ray).</summary>
        public static bool Sees(Vector3 eye, Vector3 forward, Vector3 bot, bool clear)
        {
            var to = bot - eye;
            if (to.magnitude > SeenAt || !clear) return false;
            forward.y = 0f;
            to.y = 0f;
            return forward.sqrMagnitude < 1e-4f || Vector3.Angle(forward, to) <= SeenAngle / 2f;
        }

        /// <summary>One step along a route: where it is now after moving <paramref name="step"/> metres (index: the point it heads to).</summary>
        public static Vector3 Advance(List<Vector3> route, ref int index, Vector3 at, float step)
        {
            while (index < route.Count && step > 0f)
            {
                var to = route[index];
                float d = Vector3.Distance(at, to);
                if (d > step) return Vector3.MoveTowards(at, to, step);
                at = to;
                step -= d;
                index++;
            }
            return at;
        }

        static float Flat(Vector3 d) => new Vector2(d.x, d.z).magnitude;
    }
}
