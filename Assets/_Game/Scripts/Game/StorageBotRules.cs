using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>What the team's storage robot is doing (replicated, append only).</summary>
    public enum BotMode : byte { Waiting, Coming, ToGrate, InDucts, ToExit, Gone, WaitingAbove, Hiding }

    /// <summary>
    /// What the raccoon's body is doing (the server decides, every peer plays the matching take): on the floor, up or down
    /// the basement shaft's ladder, leaping up into a ceiling grate (or into the car's trunk), dropping out of one (or out
    /// of the trunk), crawling in a duct, curled up asleep in the trunk, sat on its bottom where it was called (waiting
    /// for the next word). Append only (replicated).
    /// </summary>
    public enum BotMotion : byte { Floor, Ladder, VentUp, VentDown, Duct, Curled, Sit }

    /// <summary>One stretch of the storage robot's way.</summary>
    public enum BotLegKind : byte { Walk, ClimbUp, Crawl, ClimbDown, LeaveByHatch, HopOut, HopIn }

    public struct BotLeg
    {
        public BotLegKind Kind;
        /// <summary>Walk: where to; ClimbUp/ClimbDown: the grate's middle on the ceiling; HopOut: the floor beside the car; HopIn: the trunk.</summary>
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
    /// the duct, its stash in reach of a player standing under the grate. Out of the ducts it walks only within the room it
    /// stands in (<see cref="SameRoom"/>: to the player beside it, to the exit hatch of this room) and the last stretch
    /// under a grate; to anywhere else it goes up into the ducts (<see cref="Way"/>). Sent to the exit (once the exit room is seen) it crawls to
    /// the exit room and leaves by the exit hatch with its load; on an alarm it hides in the basement. Robots that see it in
    /// a room raise the alarm.
    /// </summary>
    public static class StorageBotRules
    {
        /// <summary>The stash board shows and the fuel burns within this of the robot, metres.</summary>
        public const float StashReach = 2.4f;
        /// <summary>Called, it stops this close to the player who called it.</summary>
        public const float Arrive = 1.6f;
        /// <summary>
        /// The longest way it walks on the floor, metres: a whole house from the car's trunk (the basement, up the shaft,
        /// across the storeys) — at 45 m a call from the trunk to a far room always sent it up into the ducts.
        /// </summary>
        public const float WalkFurthest = 140f;

        /// <summary>How it gets somewhere.</summary>
        public enum Route { Walk, Ducts, None }

        /// <summary>
        /// Where it stands and where it is going are one room (or both the basement it arrived in): it walks over. A point
        /// in no room (a doorway, outside) is in none.
        /// </summary>
        public static bool SameRoom(LevelLayout l, Vector3 from, Vector3 to)
        {
            if (l == null) return false;
            bool a = Basement.Contains(l.Cellar, from), b = Basement.Contains(l.Cellar, to);
            if (a || b) return a && b;
            var ra = l.RoomAt(from + Vector3.up * 0.1f);
            var rb = l.RoomAt(to + Vector3.up * 0.1f);
            return ra != null && rb != null && ra.Id == rb.Id && ra.Floor == rb.Floor;
        }

        /// <summary>Up to this far it always runs on the floor, metres (more than a few rooms).</summary>
        public const float WalkNear = 30f;
        /// <summary>Further than that it still runs unless the floor is longer than the ducts' way (with <see cref="DuctOverhead"/>) times this.</summary>
        public const float DuctDetour = 2f;
        /// <summary>The leap up into a grate and the drop out of one (crouch, hang, the climb), as metres of running.</summary>
        public const float DuctOverhead = 12f;

        /// <summary>
        /// The way it takes (pure): on foot whenever the floor leads there — in its own room, within <see cref="WalkNear"/>, or
        /// not more than <see cref="DuctDetour"/> times the ducts' way with the leap up and the drop down
        /// (<see cref="DuctOverhead"/>); through the ducts only when the floor does not lead there (a locked door, it is up
        /// in them already) or goes round twice as far.
        /// </summary>
        /// <param name="sameRoom">Start and end are in one room (<see cref="SameRoom"/>).</param>
        /// <param name="floorWay">The length of the way on the floor, metres; negative: there is none (or it is up in the ducts).</param>
        /// <param name="ductWay">The length of the way through the ducts, metres; negative: they do not lead there (or a grate is shut).</param>
        public static Route Way(bool sameRoom, float floorWay, float ductWay)
        {
            bool walkable = floorWay >= 0f && floorWay <= WalkFurthest;
            if (walkable && (sameRoom || floorWay <= WalkNear || ductWay < 0f || floorWay <= (ductWay + DuctOverhead) * DuctDetour)) return Route.Walk;
            return ductWay >= 0f ? Route.Ducts : Route.None;
        }

        /// <summary>
        /// The length of a way of legs from <paramref name="from"/> (pure; the walks straight, the crawls along the duct,
        /// the climbs up and down), metres — to weigh the ducts against the floor.
        /// </summary>
        public static float Length(Vector3 from, IReadOnlyList<BotLeg> legs)
        {
            float sum = 0f;
            var at = from;
            foreach (var leg in legs)
            {
                switch (leg.Kind)
                {
                    case BotLegKind.Crawl:
                        foreach (var p in leg.Route) { sum += Vector3.Distance(at, p); at = p; }
                        break;
                    case BotLegKind.ClimbUp:
                    case BotLegKind.ClimbDown:
                        sum += Flat(leg.To - at) + Mathf.Abs(leg.To.y - at.y);
                        at = leg.To;
                        break;
                    default:
                        sum += Flat(leg.To - at);
                        at = leg.To;
                        break;
                }
            }
            return sum;
        }

        /// <summary>The house-wide index of a storey's grate (the grates of every storey counted in order: the lid's open-key, <see cref="OpenKeys.Vent"/>).</summary>
        public static int GrateIndex(IReadOnlyList<VentNet> vents, int floor, int i)
        {
            int index = i;
            for (int f = 0; f < floor && f < vents.Count; f++) index += vents[f].Grates.Count;
            return index;
        }

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

        // ------------------------------------------------------------------ the raccoon's body

        /// <summary>Up into a grate: it crouches this long, then leaps (seconds; the VentUp take crouches as long).</summary>
        public const float LeapCrouch = 0.3f;
        /// <summary>The leap from the floor into the duct, seconds.</summary>
        public const float LeapTime = 0.55f;
        /// <summary>Out of a grate: it hangs nose down this long, then drops (seconds; the VentDown take matches).</summary>
        public const float DropHang = 0.3f;
        /// <summary>The drop from the duct to the floor, seconds.</summary>
        public const float DropTime = 0.45f;
        /// <summary>Out of the car's trunk and back into it: the drop (<see cref="DropLower"/>) and the leap (<see cref="LeapLift"/>) carried sideways to the floor beside the tailgate, metres behind it.</summary>
        public const float TailgateStep = 1.0f;

        /// <summary>The reaction to a pat on the head (the Pet take), seconds.</summary>
        public const float PetTime = 2.2f;

        /// <summary>The share of the way up into the duct <paramref name="t"/> seconds after it started at the grate: still while it crouches, then a leap that slows at the top.</summary>
        public static float LeapLift(float t)
        {
            float u = Mathf.Clamp01((t - LeapCrouch) / LeapTime);
            return 1f - (1f - u) * (1f - u);
        }

        /// <summary>The share of the way down to the floor <paramref name="t"/> seconds after it started: still while it hangs, then a fall that quickens.</summary>
        public static float DropLower(float t)
        {
            float u = Mathf.Clamp01((t - DropHang) / DropTime);
            return u * u;
        }

        /// <summary>Up or down the basement shaft's ladder (the navmesh link), metres a second.</summary>
        public const float LadderSpeed = 1.1f;
        /// <summary>The takes' own speeds (Tools/Blender/raccoon.py): the walk's and the bound's feet keep pace with the floor at these, the climb with the rungs, m/s.</summary>
        public const float WalkTakeSpeed = 0.55f, RunTakeSpeed = 2.4f, ClimbTakeSpeed = 1.0f;

        /// <summary>The raccoon's takes (the animator's states of the same names).</summary>
        public enum Take : byte { Move, Climb, VentUp, VentDown, Pet, Curl, Sit }

        /// <summary>The take for what its body does; a pat shows only on the floor (standing or sat).</summary>
        public static Take TakeOf(BotMotion motion, bool petted) => motion switch
        {
            BotMotion.Ladder => Take.Climb,
            BotMotion.VentUp => Take.VentUp,
            BotMotion.VentDown => Take.VentDown,
            BotMotion.Floor or BotMotion.Sit when petted => Take.Pet,
            BotMotion.Curled => Take.Curl,
            BotMotion.Sit => Take.Sit,
            _ => Take.Move,
        };

        /// <summary>
        /// It sits down: it has come to where it was called (the call's walk is over), it is on the floor and has nothing
        /// more to do — and it stays sat until the next word sets it off.
        /// </summary>
        public static bool SitsDown(bool called, bool onFloor, bool legsLeft) => called && onFloor && !legsLeft;

        // ------------------------------------------------------------------ the head turned to a player

        /// <summary>
        /// How it turns its head (pure): towards a player while it sits, <c>x</c> degrees of yaw (positive to its right) and
        /// <c>y</c> of pitch (positive up), within the neck's reach. A player out of its look's range or behind it — further
        /// round than <see cref="MuleSettings.NeckGiveUp"/> — gets no turn at all (the neck never twists round to the back);
        /// one off to the side past the neck's reach is followed only as far as it goes.
        /// </summary>
        /// <param name="forward">Where its body faces (flat is enough).</param>
        /// <param name="head">Its head.</param>
        /// <param name="target">The player's eyes.</param>
        public static Vector2 HeadTurn(Vector3 forward, Vector3 head, Vector3 target, MuleSettings s)
        {
            s ??= new MuleSettings();
            var to = target - head;
            var flatTo = new Vector3(to.x, 0f, to.z);
            var flatForward = new Vector3(forward.x, 0f, forward.z);
            if (to.magnitude > s.LookRange || flatTo.sqrMagnitude < 1e-4f || flatForward.sqrMagnitude < 1e-6f) return Vector2.zero;
            float yaw = Vector3.SignedAngle(flatForward, flatTo, Vector3.up);
            if (Mathf.Abs(yaw) > s.NeckGiveUp) return Vector2.zero;
            float pitch = Mathf.Atan2(to.y, flatTo.magnitude) * Mathf.Rad2Deg;
            return new Vector2(Mathf.Clamp(yaw, -s.NeckYaw, s.NeckYaw), Mathf.Clamp(pitch, -s.NeckPitch, s.NeckPitch));
        }

        // ------------------------------------------------------------------ on its paws, up the shaft's ladder

        /// <summary>
        /// How far to lower (or raise) the body so its paws stand on the floor under it, metres (pure): the navmesh it walks
        /// on lies a little above the real floor. No floor under it, or one further than a step away — the shaft's hole, a
        /// stair's edge — leaves it as it is.
        /// </summary>
        public static float Ground(float body, float? floor)
        {
            if (floor is not float f) return 0f;
            float d = f - body;
            return d < -GroundReach || d > GroundLift ? 0f : d;
        }

        /// <summary>Its paws reach down to a floor at most this far under its body, and up at most <see cref="GroundLift"/> (metres).</summary>
        public const float GroundReach = 0.3f, GroundLift = 0.1f;

        /// <summary>
        /// Up or down the shaft's ladder its middle is this far past the rungs (towards the wall, metres): the Climb take
        /// holds the forepaws about this far behind its middle (Tools/Blender/raccoon.py, measured), so they grip the rungs.
        /// </summary>
        public const float ClimbGrip = 0.12f;

        /// <summary>
        /// Its way along the shaft's navmesh link (pure): across to the ladder at the height it starts at, straight up or
        /// down the rungs (<paramref name="rungs"/>: on the rungs' line; <paramref name="facing"/>: towards them, flat), off
        /// to the link's end. The leg from the first point to the second is the climb; the one along the top, over the
        /// shaft's hole, is a hop (<see cref="HopArc"/>).
        /// </summary>
        public static Vector3[] LadderWay(Vector3 from, Vector3 to, Vector3 rungs, Vector3 facing)
        {
            facing.y = 0f;
            facing = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
            var grip = rungs + facing * ClimbGrip;
            return new[] { new Vector3(grip.x, from.y, grip.z), new Vector3(grip.x, to.y, grip.z), to };
        }

        /// <summary>The hop's height over its straight line, metres, at share <paramref name="u"/> of a hop <paramref name="length"/> long.</summary>
        public static float HopArc(float u, float length) => Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * Mathf.Min(0.35f, length * 0.2f);

        /// <summary>Across to the ladder and off it (hopping over the hole at the top), metres a second.</summary>
        public const float LadderStepSpeed = 1.6f;

        /// <summary>How fast the walking takes play at a floor speed: as is up to the bound's own speed, quicker beyond it.</summary>
        public static float Stride(float speed) => Mathf.Max(1f, speed / RunTakeSpeed);

        /// <summary>How fast the climb plays at a vertical speed: backwards on the way down, still while hanging.</summary>
        public static float ClimbRate(float verticalSpeed) => Mathf.Clamp(verticalSpeed / ClimbTakeSpeed, -3f, 3f);

        /// <summary>A player may pet it: on the floor (waiting, sat down, or walking over to someone), not up in a duct.</summary>
        public static bool CanPet(BotMode mode, BotMotion motion) => mode is BotMode.Waiting or BotMode.Coming && motion is BotMotion.Floor or BotMotion.Sit;

        // ------------------------------------------------------------------ sounds (StorageBotSounds)

        /// <summary>A one-off sound of Linus.</summary>
        public enum Cue : byte { None, Leap, Drop, Hop, Chatter, Squeak }

        /// <summary>A paw on the floor every this many metres; the backpack clinks every <see cref="PackEvery"/> paws; a duct's steel gives every <see cref="DuctKnockSpacing"/>.</summary>
        public const float PawSpacing = 0.22f, DuctKnockSpacing = 0.45f;
        public const int PackEvery = 3;
        /// <summary>Asleep: a snore every this many seconds; sat waiting: a sniff between these.</summary>
        public const float SnoreEvery = 3.8f, SniffMin = 5f, SniffMax = 11f;

        /// <summary>
        /// The sound of his body changing what it does (pure): the leap up into a grate, the drop out of one, the hop into
        /// or out of the car's bed.
        /// </summary>
        public static Cue CueOf(BotMotion was, BotMotion now)
        {
            if (was == now) return Cue.None;
            if (now == BotMotion.VentUp) return Cue.Leap;
            if (now == BotMotion.VentDown) return Cue.Drop;
            if (now == BotMotion.Curled || was == BotMotion.Curled) return Cue.Hop;
            return Cue.None;
        }

        /// <summary>The sound of what he is told to do (pure): his chatter answering a call, a squeak as he bolts at an alarm.</summary>
        public static Cue CueOf(BotMode was, BotMode now) =>
            was == now ? Cue.None : now == BotMode.Coming ? Cue.Chatter : now == BotMode.Hiding ? Cue.Squeak : Cue.None;

        /// <summary>A player may call it now (it is in the house, not on its way out).</summary>
        public static bool CanCall(BotMode mode) => mode is not (BotMode.ToExit or BotMode.Gone);

        /// <summary>It may be sent to the exit: the team has seen the exit room, and it is not on its way already.</summary>
        public static bool CanSendToExit(BotMode mode, bool exitSeen) => exitSeen && CanCall(mode) && mode != BotMode.ToGrate;

        /// <summary>The grate of a storey's ducts nearest a point (flat distance), or null.</summary>
        public static DuctGrate? NearestGrate(VentNet net, Vector3 p, System.Func<int, bool> open = null)
        {
            DuctGrate? best = null;
            float d = float.MaxValue;
            for (int i = 0; i < net.Grates.Count; i++)
            {
                var g = net.Grates[i];
                if (open != null && !open(i)) continue;                // (a shut lid: no way through it)
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

    /// <summary>Tuning of Linus the mule (Data/Config/Run → Mule): how he turns his head to the team while he sits and waits.</summary>
    [System.Serializable]
    public class MuleSettings
    {
        [Tooltip("Sat waiting, he turns his head to the nearest player within this, metres.")]
        public float LookRange = 8f;
        [Tooltip("The furthest his neck and head turn to the side, degrees (a player further round is followed only this far).")]
        public float NeckYaw = 60f;
        [Tooltip("A player further round than this (behind him), degrees: he does not turn his head at all — the neck never twists to the back.")]
        public float NeckGiveUp = 115f;
        [Tooltip("The furthest he looks up or down at a player, degrees.")]
        public float NeckPitch = 25f;
        [Tooltip("How fast the head turns to its aim, degrees a second.")]
        public float NeckSpeed = 150f;
    }
}
