using System;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>How doors open and how robots go through doorways (Data/Config/Level → Doors).</summary>
    [Serializable]
    public class DoorSettings
    {
        [Header("Who opens them")]
        [Tooltip("Doors open by themselves when anyone comes near and close behind them (as they did before). Off: players open and " +
                 "close them with E, robots open them by hand. Every peer needs the same value.")]
        public bool Automatic;
        [Tooltip("Seconds a robot standing at a shut door takes before it starts to open (a shut door delays a robot, it never stops it).")]
        public float RobotOpenTime = 1.1f;
        [Tooltip("Seconds after the last robot has gone before a door a robot opened shuts again. A door a player opened stays open.")]
        public float RobotShutDelay = 0.8f;

        [Header("Robots in doorways")]
        [Tooltip("A doorway is claimed by the robot coming through this far before it, metres: one coming the other way waits.")]
        public float ClaimDistance = 2.6f;
        [Tooltip("A robot that has to wait stands at least this far from the doorway, and off to the side, metres.")]
        public float WaitGap = 2.1f;
        [Tooltip("A claim lasts no longer than this, seconds (its robot has stopped in the doorway: the others try their luck).")]
        public float ClaimTime = 6f;
        [Tooltip("A robot that wants to walk but has not got anywhere for this long is stuck, seconds.")]
        public float StuckTime = 2.2f;
        [Tooltip("Stuck this close to where it was going: it has arrived, metres. Farther: for a moment it does not make way for others.")]
        public float StuckNearGoal = 2.5f;
        [Tooltip("How long a stuck robot pushes through the others, seconds.")]
        public float PushTime = 1.8f;
    }

    /// <summary>What goes on at one door this frame (server).</summary>
    public struct DoorInput
    {
        /// <summary>A robot stands at it, wanting through.</summary>
        public bool RobotWaits;
        /// <summary>The team's storage robot is at it (it does not stop: the door opens at once).</summary>
        public bool HelperComes;
        /// <summary>A robot (or the storage robot) is still near: a door opened for it stays open.</summary>
        public bool RobotNear;
        /// <summary>Someone stands in the doorway: it does not shut on them.</summary>
        public bool Occupied;
        /// <summary>Locked for players now (the storage room's door locks again by itself): it shuts once nobody needs it.</summary>
        public bool Locked;
    }

    /// <summary>
    /// One door's word, kept by the server (pure): shut until a player opens it with E or a robot has stood at it for a
    /// moment. A door a robot opened shuts behind it; one a player opened stays open until somebody shuts it.
    /// </summary>
    public sealed class DoorState
    {
        public bool Open { get; private set; }
        /// <summary>Open because of a robot: it shuts by itself.</summary>
        public bool ByRobot { get; private set; }
        /// <summary>How far a robot has got with opening it, 0..1 of <see cref="DoorSettings.RobotOpenTime"/>.</summary>
        public float Work { get; private set; }
        float m_Clear;

        /// <summary>A player pressed E at it: opens or shuts it. False: it does not shut on somebody in the doorway.</summary>
        public bool Toggle(bool occupied)
        {
            if (Open && occupied) return false;
            Open = !Open;
            ByRobot = false;
            Work = 0f;
            m_Clear = 0f;
            return true;
        }

        /// <summary>One frame; true when the door's word has changed.</summary>
        public bool Step(in DoorInput at, float dt, DoorSettings s)
        {
            if (!Open)
            {
                m_Clear = 0f;
                Work = at.RobotWaits ? Work + (s.RobotOpenTime > 0f ? dt / s.RobotOpenTime : 1f) : 0f;
                if (Work < 1f && !at.HelperComes) return false;
                Open = ByRobot = true;
                Work = 0f;
                return true;
            }
            if ((!ByRobot && !at.Locked) || at.RobotNear || at.RobotWaits || at.Occupied) { m_Clear = 0f; return false; }
            m_Clear += dt;
            if (m_Clear < s.RobotShutDelay) return false;
            Open = ByRobot = false;
            m_Clear = 0f;
            return true;
        }
    }

    /// <summary>Where a walk crosses a doorway, and how a robot stands at a shut door (pure).</summary>
    public static class DoorwayMath
    {
        /// <summary>
        /// How far along a path (<paramref name="corners"/>, the first <paramref name="count"/> of them) it goes through the
        /// doorway at <paramref name="center"/> (across <paramref name="forward"/>, <paramref name="width"/> wide), metres;
        /// −1 when not within <paramref name="look"/>. <paramref name="way"/>: +1 towards <paramref name="forward"/>, −1 back.
        /// </summary>
        public static float Crossing(Vector3 center, Vector3 forward, float width, Vector3[] corners, int count, float look, out int way)
        {
            way = 0;
            var right = new Vector3(forward.z, 0f, -forward.x);
            float walked = 0f;
            for (int i = 0; i + 1 < count && walked <= look; i++)
            {
                Vector3 a = corners[i], b = corners[i + 1];
                float length = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                float da = Vector3.Dot(a - center, forward), db = Vector3.Dot(b - center, forward);
                if ((da < 0f) != (db < 0f))
                {
                    float k = Mathf.Abs(da - db) > 1e-5f ? da / (da - db) : 0f;
                    var p = Vector3.Lerp(a, b, k);
                    if (Mathf.Abs(Vector3.Dot(p - center, right)) <= width / 2f + 0.15f && Mathf.Abs(p.y - center.y) < 1.6f)
                    {
                        float at = walked + length * k;
                        if (at > look) return -1f;
                        way = db > da ? 1 : -1;
                        return at;
                    }
                }
                walked += length;
            }
            return -1f;
        }

        /// <summary>
        /// How far from the doorway's middle a robot stands while the door opens: clear of a leaf of <paramref name="leaf"/>
        /// metres swinging towards it, else (sliding, or swinging away) just before it.
        /// </summary>
        public static float StandOff(float radius, float leaf, bool towards) =>
            towards ? Mathf.Max(0.5f, leaf) + radius + 0.15f : radius + 0.45f;

        /// <summary>
        /// Where a robot waits aside for another to come through (pure): <paramref name="gap"/> metres back from the doorway on
        /// its own side and off the way, to the side it stands on.
        /// </summary>
        public static Vector3 WaitSpot(Vector3 center, Vector3 forward, Vector3 robot, float gap)
        {
            var right = new Vector3(forward.z, 0f, -forward.x);
            float side = Vector3.Dot(robot - center, forward) >= 0f ? 1f : -1f;
            float lateral = Vector3.Dot(robot - center, right) >= 0f ? 1f : -1f;
            var spot = center + forward * (side * gap * 0.75f) + right * (lateral * gap * 0.75f);
            spot.y = robot.y;
            return spot;
        }
    }

    /// <summary>
    /// Who goes through a doorway (pure, server): the first robot there claims it with its direction; others going the same
    /// way follow, those coming the other way wait until it is free — no more noses against each other in a door.
    /// </summary>
    public sealed class DoorwayClaims
    {
        struct Claim
        {
            public int Robot, Way;
            public float Since, Until;
        }

        readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Claim>> m_Claims = new();

        public void Clear() => m_Claims.Clear();

        /// <summary>
        /// Robot <paramref name="robot"/> wants through doorway <paramref name="doorway"/> (a claim is renewed by asking
        /// again, it lapses half a second after the last time). False: someone is coming the other way — wait.
        /// </summary>
        public bool Ask(int doorway, int robot, int way, float now, DoorSettings s)
        {
            if (!m_Claims.TryGetValue(doorway, out var list)) m_Claims[doorway] = list = new System.Collections.Generic.List<Claim>();
            list.RemoveAll(c => c.Until < now);
            int mine = list.FindIndex(c => c.Robot == robot && c.Way == way);
            foreach (var c in list)
                if (c.Robot != robot && c.Way != way && now - c.Since < s.ClaimTime && (mine < 0 || c.Since < list[mine].Since)) return false;
            var claim = mine >= 0 ? list[mine] : new Claim { Robot = robot, Way = way, Since = now };
            claim.Until = now + 0.5f;
            if (mine >= 0) list[mine] = claim;
            else
            {
                list.RemoveAll(c => c.Robot == robot); // it turned round
                list.Add(claim);
            }
            return true;
        }
    }

    /// <summary>Notices that a robot which wants to walk is not getting anywhere (pure).</summary>
    public sealed class StuckMeter
    {
        Vector3 m_From;
        float m_Time;
        bool m_Started;

        /// <summary>One frame; true at the moment it has been stuck for <paramref name="after"/> seconds (then it counts anew).</summary>
        public bool Step(Vector3 at, bool wants, float dt, float after)
        {
            if (!m_Started || !wants || (at - m_From).sqrMagnitude > 0.35f * 0.35f)
            {
                m_Started = true;
                m_From = at;
                m_Time = 0f;
                return false;
            }
            m_Time += dt;
            if (m_Time < after) return false;
            m_Time = 0f;
            return true;
        }
    }
}
