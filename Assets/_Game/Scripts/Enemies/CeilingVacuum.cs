using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Robot vacuum riding the ceiling of its room upside down, bouncing off the walls. It projects a rotating red
    /// scan line onto the floor; crossing the line raises an alarm. It never attacks. Jump over the line.
    /// </summary>
    public class CeilingVacuum : SecurityDevice
    {
        [Tooltip("Tuning (VacuumProfile) — a separate asset next to the prefab.")]
        public VacuumProfile Profile;
        protected override DeviceProfile Device => Profile;
        public LineRenderer Line;

        Vector3 m_Min, m_Max, m_Dir;
        float m_FloorY;

        protected override Color IdleColor => new(1f, 0.3f, 0.3f);

        protected override void OnDeviceSpawned()
        {
            var layout = ActiveLevel.Layout;
            var c = layout != null ? layout.RoomCenterAt(transform.position) : transform.position;
            m_FloorY = c.y;
            float half = LevelMetrics.Cell / 2f - 0.8f;
            m_Min = new Vector3(c.x - half, 0, c.z - half);
            m_Max = new Vector3(c.x + half, 0, c.z + half);
            if (!IsServer) return;
            m_Dir = Quaternion.Euler(0, LevelRandom.Next(RandomChannel.Devices).Range(0f, 360f), 0) * Vector3.forward;
            transform.SetPositionAndRotation(new Vector3(c.x, CeilingY, c.z), Quaternion.Euler(180f, 0f, 0f));
        }

        float CeilingY => m_FloorY + LevelMetrics.WallHeight - 0.05f;

        Vector3 ScanDir => ScanLine.Direction(Stealth.Game.Session.ServerTimeF, Profile.ScanSpeed, NetId * 37f);

        protected override void TickLocal()
        {
            if (Line == null) return;
            // The line on the floor (both ways), drawn from the shared clock so every peer agrees.
            var c = new Vector3(transform.position.x, m_FloorY + 0.02f, transform.position.z);
            var d = ScanDir * Profile.HalfLength;
            Line.enabled = true;
            Line.SetPosition(0, c - d);
            Line.SetPosition(1, c + d);
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

            var center = new Vector3(p.x, m_FloorY, p.z);
            foreach (var pl in PlayerController.All)
            {
                if (pl.IsDown || LevelLayout.FloorOf(pl.Position.y) != LevelLayout.FloorOf(m_FloorY + 0.1f)) continue;
                if (ScanLine.Above(pl.Position.y, m_FloorY + 0.1f)) continue; // in the air
                if (ScanLine.Crosses(center, ScanDir, Profile.HalfLength, true, pl.Position)) { Trip(pl); return; }
            }
            Forget();
        }
    }
}
