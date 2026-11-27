using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// What a mobile robot does besides its rounds (RobotTraits.Role; saved in configs: append only). The numbers of a
    /// role are <see cref="RobotTraits.RolePeriod"/>, <see cref="RobotTraits.RoleRadius"/>,
    /// <see cref="RobotTraits.RoleDuration"/> and <see cref="RobotTraits.RoleLimit"/>.
    /// </summary>
    public enum RobotRole : byte
    {
        None,
        /// <summary>Shuts the doors players left open and sets hacked locks again (Limit: locks per house).</summary>
        Warden,
        /// <summary>Screws the crawl-way grilles players took off back on.</summary>
        Repairman,
        /// <summary>Every Period puffs a cloud of smoke of Radius for Duration: players see a few steps.</summary>
        Smoker,
        /// <summary>Leaves a puddle of sticky goo of Radius every Period while it rolls; it stays for Duration.</summary>
        Mopper,
        /// <summary>Breaks the shut and locked doors in its way for good while it is after someone.</summary>
        Driller,
        /// <summary>Stands still as a crate until a player tries to search it; then hunts them, loses them, and freezes in another room.</summary>
        Mimic,
        /// <summary>Picks one player and stalks them (every Period it learns roughly where they are); caught, they go to its lair and it picks the next.</summary>
        Maniac,
    }

    /// <summary>What a robot does to the player it has caught up with (RobotTraits.Attack; saved in configs: append only).</summary>
    public enum AttackKind : byte
    {
        /// <summary>A hit: the player goes down.</summary>
        Hit,
        /// <summary>Throws a net: the player stands held until they tear out of it.</summary>
        Net,
        /// <summary>A shot from a distance, when nothing is between them.</summary>
        Shoot,
        /// <summary>A sedative: the player is out whatever their health (the nurse then carries them to a bed).</summary>
        Sedate,
        /// <summary>Talks at them, loudly: a noise of Radius that every robot with ears comes to.</summary>
        Pester,
        /// <summary>Sprays a cloud of laughing gas of Radius round itself for Duration.</summary>
        Gas,
        /// <summary>An ultrasonic shriek: every player within Radius is stunned for Duration.</summary>
        Shriek,
    }

    /// <summary>What a warden does with a door.</summary>
    public enum DoorJob : byte { None, Shut, Relock }

    /// <summary>The rules of the roles and the attacks (pure, unit tested).</summary>
    public static class RoleRules
    {
        /// <summary>Attacks that work from a distance and need a clear line; the robot stands to make them.</summary>
        public static bool Ranged(AttackKind kind) => kind is AttackKind.Net or AttackKind.Shoot or AttackKind.Shriek or AttackKind.Gas or AttackKind.Pester;

        /// <summary>A wall between them stops it (a shriek, gas and talk go round corners).</summary>
        public static bool NeedsLine(AttackKind kind) => kind is AttackKind.Hit or AttackKind.Net or AttackKind.Shoot or AttackKind.Sedate;

        /// <summary>The periodic thing is due (the first one a period after the start).</summary>
        public static bool Due(float now, float last, float period) => period > 0f && now - last >= period;

        /// <summary>
        /// What a warden does with a door: one standing open by a player's hand it shuts; a lock the team hacked it sets
        /// again, while it has locks left. A door a robot holds open, a vault, a key door — not its business.
        /// </summary>
        public static DoorJob Warden(bool open, bool byRobot, bool hasLock, bool lockHacked, bool special, int locksLeft)
        {
            if (special) return DoorJob.None;
            if (open && !byRobot) return DoorJob.Shut;
            if (!open && hasLock && lockHacked && locksLeft > 0) return DoorJob.Relock;
            return DoorJob.None;
        }

        /// <summary>The nearest of the spots within <paramref name="radius"/> on the robot's floor, −1 if none.</summary>
        public static int Nearest(Vector3 from, IReadOnlyList<Vector3> spots, float radius)
        {
            int best = -1;
            float bestDistance = radius;
            for (int i = 0; i < spots.Count; i++)
            {
                if (Mathf.Abs(spots[i].y - from.y) > 1.6f) continue;
                float d = Vector3.Distance(spots[i], from);
                if (d <= bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Whom a maniac stalks next: the first of the players (in the order of their ids, starting at a seeded place) who is
        /// up and about; none up — nobody (−1).
        /// </summary>
        public static int NextPrey(IReadOnlyList<bool> up, int start)
        {
            int n = up.Count;
            for (int k = 0; k < n; k++)
            {
                int i = ((start % n) + n + k) % n;
                if (up[i]) return i;
            }
            return -1;
        }

        /// <summary>Work this high above its feet (metres) a repairman goes up the wall for.</summary>
        public static bool Climbs(float height) => height > 1.2f;

        /// <summary>Seconds a robot spends at its work once it is there (longer up a wall).</summary>
        public static float WorkTime(bool climbing) => climbing ? 4f : 1.2f;

        /// <summary>How far up the wall a climbing body is, <paramref name="t"/> 0 … 1 of the climb eased (pure): 0 on the floor, 1 at the top.</summary>
        public static float ClimbEase(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        /// <summary>
        /// A driller breaks the wall ahead when the way round is much longer than the way through (or there is none):
        /// <paramref name="straight"/> metres to where its prey is, <paramref name="path"/> metres by the floor.
        /// </summary>
        public static bool WorthBreaking(float straight, float path, bool noWay) =>
            straight < 14f && (noWay || float.IsInfinity(path) || path > straight * 2f + 4f);

        /// <summary>Whom a shriek or a cloud reaches: within the radius on the same floor.</summary>
        public static bool Reaches(Vector3 from, Vector3 to, float radius) =>
            Mathf.Abs(to.y - from.y) <= Player.Hazards.Height && new Vector2(to.x - from.x, to.z - from.z).sqrMagnitude <= radius * radius;
    }
}
