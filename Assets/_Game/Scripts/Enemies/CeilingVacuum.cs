using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Robot vacuum riding the ceiling of its room upside down, bouncing off the walls. It projects a rotating laser fan
    /// down to the floor — a vertical sheet of light from the robot to a line on the floor, cut short by walls. Touching the
    /// sheet with any part of the body raises an alarm (it is not a tripwire: jumping does not help). It never attacks.
    /// Keep out of its reach, slip past while it turns away, or hack it.
    /// </summary>
    public class CeilingVacuum : SecurityDevice
    {
        [Tooltip("Tuning (VacuumProfile) — a separate asset next to the prefab.")]
        public VacuumProfile Profile;
        protected override DeviceProfile Device => Profile;
        public LineRenderer Line;
        [Tooltip("The two halves of the laser fan: a right-triangle mesh, apex at the origin, its floor edge along +X (scaled each frame).")]
        public Transform FanA, FanB;
        public Renderer FanARenderer, FanBRenderer;

        /// <summary>The fan starts this far under the robot's centre (its lens).</summary>
        const float LensDrop = 0.12f;
        /// <summary>Walls cut the fan: the ray that finds them runs at this height over the floor (over desks, under shelves).</summary>
        const float CutHeight = 1.2f;

        Vector3 m_Min, m_Max, m_Dir;
        float m_FloorY;

        protected override Color IdleColor => new(1f, 0.3f, 0.3f);

        protected override void OnDeviceSpawned()
        {
            var layout = ActiveLevel.Layout;
            var rect = layout != null ? layout.RoomRectAt(transform.position) : new Rect(transform.position.x - 5f, transform.position.z - 5f, 10f, 10f);
            var c = new Vector3(rect.center.x, layout != null ? LevelLayout.FloorY(LevelLayout.FloorOf(transform.position.y)) : transform.position.y, rect.center.y);
            m_FloorY = c.y;
            m_Min = new Vector3(rect.xMin + 0.8f, 0, rect.yMin + 0.8f);
            m_Max = new Vector3(Mathf.Max(rect.xMin + 0.8f, rect.xMax - 0.8f), 0, Mathf.Max(rect.yMin + 0.8f, rect.yMax - 0.8f));
            if (!IsServer) return;
            m_Dir = Quaternion.Euler(0, LevelRandom.Next(RandomChannel.Devices).Range(0f, 360f), 0) * Vector3.forward;
            transform.SetPositionAndRotation(new Vector3(c.x, CeilingY, c.z), Quaternion.Euler(180f, 0f, 0f));
        }

        float CeilingY => m_FloorY + LevelMetrics.WallHeight - 0.05f;

        Vector3 ScanDir => ScanLine.Direction(Stealth.Game.Session.ServerTimeF, Profile.ScanSpeed, NetId * 37f);

        /// <summary>How far the fan reaches along <paramref name="dir"/> from the point under the robot before a wall.</summary>
        float Reach(Vector3 floor, Vector3 dir)
        {
            float max = Profile.HalfLength;
            return Physics.Raycast(floor + Vector3.up * CutHeight, dir, out var hit, max, Perception.SightMask, QueryTriggerInteraction.Ignore)
                ? hit.distance : max;
        }

        Vector3 Apex => transform.position + Vector3.down * LensDrop;

        protected override void TickLocal()
        {
            if (Line == null) return;
            // Drawn from the shared clock so every peer agrees: the line on the floor and the sheet of light above it.
            var dir = ScanDir;
            var c = new Vector3(transform.position.x, m_FloorY + 0.02f, transform.position.z);
            float a = Reach(c, dir), b = Reach(c, -dir);
            Line.enabled = true;
            Line.SetPosition(0, c - dir * b);
            Line.SetPosition(1, c + dir * a);
            bool on = !IsHacked;
            PlaceFan(FanA, FanARenderer, dir, a, on);
            PlaceFan(FanB, FanBRenderer, -dir, b, on);
        }

        void PlaceFan(Transform fan, Renderer r, Vector3 dir, float length, bool on)
        {
            if (fan == null) return;
            if (r != null && r.enabled != on) r.enabled = on;
            if (!on) return;
            var apex = Apex;
            float height = Mathf.Max(0.1f, apex.y - (m_FloorY + 0.02f));
            // local +X along the floor edge, +Y up: forward = cross(dir, up) makes right = dir
            fan.SetPositionAndRotation(apex, Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up));
            var parent = fan.parent != null ? fan.parent.lossyScale : Vector3.one;
            fan.localScale = new Vector3(Mathf.Max(0.01f, length) / Mathf.Max(1e-4f, Mathf.Abs(parent.x)), height / Mathf.Max(1e-4f, Mathf.Abs(parent.y)), 1f);
        }

        protected override void TickServer()
        {
            // Bounce around the ceiling.
            var p = transform.position + m_Dir * Profile.Speed * Time.deltaTime;
            if (p.x < m_Min.x || p.x > m_Max.x) { m_Dir.x = -m_Dir.x; p.x = Mathf.Clamp(p.x, m_Min.x, m_Max.x); }
            if (p.z < m_Min.z || p.z > m_Max.z) { m_Dir.z = -m_Dir.z; p.z = Mathf.Clamp(p.z, m_Min.z, m_Max.z); }
            p.y = CeilingY;
            transform.position = p;
            transform.rotation = Quaternion.Euler(180f, transform.eulerAngles.y + 40f * Time.deltaTime, 0f);

            var dir = ScanDir;
            var floor = new Vector3(p.x, m_FloorY, p.z);
            float a = Reach(floor, dir), b = Reach(floor, -dir);
            var apex = Apex;
            foreach (var pl in PlayerController.All)
            {
                if (pl.IsDown || LevelLayout.FloorOf(pl.Position.y) != LevelLayout.FloorOf(m_FloorY + 0.1f)) continue;
                if (ScanLine.CutsFan(apex, dir, m_FloorY, a, b, pl.Position, pl.Height)) { Trip(pl); return; }
            }
            Forget();
        }
    }
}
