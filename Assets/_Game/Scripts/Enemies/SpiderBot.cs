using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Wall crawler: runs around its room along the walls (above the doors), watching the floor below.
    /// On detection it leaps onto the player, hits on landing and crawls back up. Never leaves its room.
    /// </summary>
    public class SpiderBot : SecurityDevice
    {
        public float CrawlHeight = 2.6f;
        public float CrawlSpeed = 1.3f;
        public float Range = 9f;
        public float ViewAngle = 120f;
        public float LeapTime = 0.7f;
        public float ReturnTime = 1.3f;
        public float Cooldown = 3f;

        enum Mode { Crawl, Leap, Return }

        Mode m_Mode;
        Vector3 m_Min, m_Max;
        float m_FloorY, m_T, m_Dir = 1f, m_ModeStart, m_NextLeap, m_PauseUntil;
        Vector3 m_From, m_To;
        IIntruder m_Prey;

        public float Perimeter => 2f * ((m_Max.x - m_Min.x) + (m_Max.z - m_Min.z));

        protected override Color IdleColor => new(0.6f, 1f, 0.3f);

        protected override void OnDeviceSpawned()
        {
            if (!IsServer) return;
            var layout = ActiveLevel.Layout;
            var c = layout != null ? layout.RoomCenterAt(transform.position) : transform.position;
            float half = LevelMetrics.Cell / 2f - 0.35f;
            m_FloorY = c.y;
            m_Min = new Vector3(c.x - half, 0, c.z - half);
            m_Max = new Vector3(c.x + half, 0, c.z + half);
            m_T = Random.value * Perimeter;
            var (p, rot) = OnWall(m_T, m_Dir);
            transform.SetPositionAndRotation(p, rot);
        }

        /// <summary>Point on the room's wall loop at distance <paramref name="t"/>, oriented legs-to-wall.</summary>
        public (Vector3 pos, Quaternion rot) OnWall(float t, float dir)
        {
            float w = m_Max.x - m_Min.x, d = m_Max.z - m_Min.z;
            t = Mathf.Repeat(t, 2f * (w + d));
            Vector3 p, tangent, inward;
            if (t < w) { p = new Vector3(m_Min.x + t, 0, m_Min.z); tangent = Vector3.right; inward = Vector3.forward; }
            else if (t < w + d) { p = new Vector3(m_Max.x, 0, m_Min.z + (t - w)); tangent = Vector3.forward; inward = Vector3.left; }
            else if (t < 2 * w + d) { p = new Vector3(m_Max.x - (t - w - d), 0, m_Max.z); tangent = Vector3.left; inward = Vector3.back; }
            else { p = new Vector3(m_Min.x, 0, m_Max.z - (t - 2 * w - d)); tangent = Vector3.back; inward = Vector3.right; }
            p.y = m_FloorY + CrawlHeight;
            return (p, Quaternion.LookRotation(tangent * dir, inward));
        }

        Vector3 LookDirection => Quaternion.AngleAxis(35f, Vector3.Cross(transform.up, Vector3.up).normalized) * transform.up;

        protected override void TickServer()
        {
            switch (m_Mode)
            {
                case Mode.Crawl:
                    if (Time.time > m_PauseUntil)
                    {
                        m_T += CrawlSpeed * m_Dir * Time.deltaTime;
                        if (Random.value < 0.002f) { m_Dir = -m_Dir; m_PauseUntil = Time.time + Random.Range(0.5f, 2f); }
                    }
                    var (p, rot) = OnWall(m_T, m_Dir);
                    transform.SetPositionAndRotation(p, rot);
                    Look();
                    break;

                case Mode.Leap:
                {
                    float k = Mathf.Clamp01((Time.time - m_ModeStart) / LeapTime);
                    // Mild homing: a sidestep at the last moment still works, standing still does not.
                    if (m_Prey != null) m_To = Vector3.MoveTowards(m_To, m_Prey.Position + Vector3.up * 0.2f, 2.2f * Time.deltaTime);
                    var pos = Vector3.Lerp(m_From, m_To, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                    transform.position = pos;
                    var flat = m_To - m_From;
                    flat.y = 0;
                    if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                    if (k >= 1f)
                    {
                        if (m_Prey != null && !m_Prey.IsDown && Vector3.Distance(m_Prey.Position, transform.position) < 1.5f)
                            m_Prey.HitServer(transform.position);
                        m_Mode = Mode.Return;
                        m_ModeStart = Time.time;
                        m_From = transform.position;
                    }
                    break;
                }

                case Mode.Return:
                {
                    float k = Mathf.Clamp01((Time.time - m_ModeStart) / ReturnTime);
                    var (wall, wallRot) = OnWall(m_T, m_Dir);
                    transform.SetPositionAndRotation(Vector3.Lerp(m_From, wall, k), Quaternion.Slerp(transform.rotation, wallRot, k));
                    if (k >= 1f) m_Mode = Mode.Crawl;
                    break;
                }
            }
        }

        void Look()
        {
            IIntruder seen = null;
            float best = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown || !Perception.SameFloor(p.Position, new Vector3(0, m_FloorY, 0))) continue;
                var target = p.Position + Vector3.up;
                float d = Vector3.Distance(transform.position, target);
                if (d < best && Perception.InCone(transform.position, LookDirection, target, Range, ViewAngle) && Perception.LineOfSight(transform.position + transform.up * 0.3f, p))
                { seen = p; best = d; }
            }
            if (seen == null) { Decay(0.5f); return; }
            Notice(seen, 1.6f * (seen.IsCrouching ? 0.6f : 1f) * Perception.LightFactor(seen.Position));
        }

        protected override void OnDetected(IIntruder p)
        {
            if (Time.time < m_NextLeap) return;
            m_NextLeap = Time.time + Cooldown + LeapTime + ReturnTime;
            m_Prey = p;
            m_Mode = Mode.Leap;
            m_ModeStart = Time.time;
            m_From = transform.position;
            m_To = p.Position + Vector3.up * 0.2f;
        }
    }
}
