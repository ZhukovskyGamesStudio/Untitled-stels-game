using Stealth.Game;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Floor lidar of a mobile robot (the sweeper): a red laser line spinning around it at ankle height, stopped by walls and
    /// furniture. Feet on the floor that cross the line are noticed; a player standing on a chair or a desk, or in mid-jump,
    /// is above it. The direction comes from the shared clock, so every peer draws the same line the server senses with.
    /// </summary>
    public class RobotLidar : MonoBehaviour
    {
        public RobotController Robot;
        public LineRenderer Line;
        [Tooltip("Part of the model that turns with the line (its origin on the robot's vertical axis).")]
        public Transform Head;

        static readonly RaycastHit[] s_Hits = new RaycastHit[12];

        int m_Frame = -1;
        Vector3 m_Start, m_Dir;
        float m_Length;

        RobotSenses Senses => Robot.Senses;

        /// <summary>The line this frame: start (ankle height above the robot's floor), direction, length up to the first obstacle.</summary>
        public (Vector3 start, Vector3 dir, float length) Beam()
        {
            if (m_Frame == Time.frameCount) return (m_Start, m_Dir, m_Length);
            m_Frame = Time.frameCount;
            m_Dir = ScanLine.Direction(Session.ServerTimeF, Senses.LidarTurnSpeed, Robot.NetId * 41f);
            m_Start = Robot.transform.position + Vector3.up * Senses.LidarHeight;
            m_Length = Senses.LidarRange;
            int n = Physics.RaycastNonAlloc(m_Start, m_Dir, s_Hits, Senses.LidarRange, Perception.SightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = s_Hits[i].collider;
                // Players do not stop the line (it is their feet it is looking for), nor does the robot itself.
                if (c.GetComponentInParent<IIntruder>() != null || c.transform.IsChildOf(Robot.transform)) continue;
                m_Length = Mathf.Min(m_Length, s_Hits[i].distance);
            }
            return (m_Start, m_Dir, m_Length);
        }

        /// <summary>Do these feet cross the line (on the robot's floor and not above it)?</summary>
        public bool Touches(Vector3 feet)
        {
            var (start, dir, length) = Beam();
            if (!Perception.SameFloor(feet, Robot.transform.position)) return false;
            if (ScanLine.Above(feet.y, start.y)) return false; // on a chair, a desk, in the air
            return ScanLine.Crosses(start, dir, length, false, feet);
        }

        void LateUpdate()
        {
            if (Robot == null || Robot.Profile == null || Senses.LidarRange <= 0f) return;
            var (start, dir, length) = Beam();
            bool on = !Robot.IsHacked;
            if (Head != null) Head.rotation = Quaternion.LookRotation(dir, Vector3.up);
            if (Line == null) return;
            Line.enabled = on;
            if (!on) return;
            Line.SetPosition(0, start);
            Line.SetPosition(1, start + dir * length);
        }
    }
}
