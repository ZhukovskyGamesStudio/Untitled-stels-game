using System.Collections.Generic;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Robot vacuum riding the ceiling of its room upside down, bouncing off the walls. It projects a rotating laser fan
    /// down to the floor — a vertical sheet of light from the robot's lens, each of its rays stopping at the first thing it
    /// meets (a wall, a desk, the floor), so the sheet takes the shape of what it shines on and never passes a wall. Touching the
    /// sheet with any part of the body raises an alarm (it is not a tripwire: jumping does not help). It never attacks.
    /// Keep out of its reach, slip past while it turns away, or hack it.
    /// </summary>
    public class CeilingVacuum : SecurityDevice
    {
        [Tooltip("Tuning (VacuumProfile) — a separate asset next to the prefab.")]
        public VacuumProfile Profile;
        protected override DeviceProfile Device => Profile;
        [Tooltip("The two halves of the laser fan (their meshes are rebuilt every frame from the rays: apex at the origin, local +X along the scan, +Y up).")]
        public Transform FanA, FanB;
        public Renderer FanARenderer, FanBRenderer;

        /// <summary>The fan starts this far under the robot's centre (its lens).</summary>
        const float LensDrop = 0.12f;

        readonly Vector2[] m_HalfA = new Vector2[ScanLine.FanRays], m_HalfB = new Vector2[ScanLine.FanRays];
        Vector3 m_RayApex, m_RayDir = Vector3.forward;
        Mesh m_MeshA, m_MeshB;
        static readonly RaycastHit[] s_Hits = new RaycastHit[64]; // a ray across a furnished room meets dozens of part colliders; a short buffer can drop the nearest
        static readonly List<Vector3> s_Verts = new();
        static readonly List<Vector2> s_Uvs = new();
        static readonly List<int> s_Tris = new();

        Vector3 m_Min, m_Max, m_Dir;
        float m_FloorY;
        // the body's own spin: Euler(180, y, 0) reads back as (0, y + 180, 180), so eulerAngles.y cannot carry it
        float m_Yaw;

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

        Vector3 Apex => transform.position + Vector3.down * LensDrop;

        /// <summary>
        /// Casts the rays of one half of the fan along <paramref name="dir"/> and writes where each stops (fan-plane points,
        /// see <see cref="ScanLine.FanRay"/>): the first wall, piece of furniture or door it meets, else the floor. Players
        /// do not stop the light (it is drawn over them and catches them).
        /// </summary>
        void CastHalf(Vector3 apex, Vector3 dir, Vector2[] half)
        {
            float span = apex.y - (m_FloorY + 0.01f);
            for (int i = 0; i < half.Length; i++)
            {
                var r = ScanLine.FanRay(i, half.Length, Profile.HalfLength, span, out float length);
                var world = dir * r.x + Vector3.up * r.y;
                float stop = length;
                int n = Physics.RaycastNonAlloc(apex, world, s_Hits, length, Perception.SightMask, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    var h = s_Hits[k];
                    if (h.distance >= stop || h.collider is CharacterController || h.collider.transform.IsChildOf(transform)) continue;
                    stop = h.distance;
                }
                half[i] = r * stop;
            }
        }

        // From the shared clock so every peer agrees; the server's check uses the same rays.
        protected override void TickLocal() => Sweep(ScanDir);

        /// <summary>The fan along <paramref name="dir"/> this frame: its rays cast, both halves of the sheet drawn.</summary>
        public void Sweep(Vector3 dir)
        {
            var apex = Apex;
            CastHalf(apex, dir, m_HalfA);
            CastHalf(apex, -dir, m_HalfB);
            m_RayApex = apex;
            m_RayDir = dir;
            bool on = !IsHacked;
            PlaceFan(FanA, FanARenderer, ref m_MeshA, apex, dir, m_HalfA, on);
            PlaceFan(FanB, FanBRenderer, ref m_MeshB, apex, -dir, m_HalfB, on);
        }

        /// <summary>One half of the sheet of light: a fan of triangles from the lens through the rays' ends.</summary>
        void PlaceFan(Transform fan, Renderer r, ref Mesh mesh, Vector3 apex, Vector3 dir, Vector2[] half, bool on)
        {
            if (fan == null) return;
            if (r != null && r.enabled != on) r.enabled = on;
            if (!on) return;
            if (mesh == null && fan.TryGetComponent<MeshFilter>(out var mf))
            {
                mesh = new Mesh { name = "LaserFan" };
                mesh.MarkDynamic();
                mf.sharedMesh = mesh;
            }
            if (mesh == null) return;
            // local +X along the scan, +Y up: forward = cross(dir, up) makes right = dir; no scale, the mesh is in metres
            fan.SetPositionAndRotation(apex, Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up));
            var parent = fan.parent != null ? fan.parent.lossyScale : Vector3.one;
            fan.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(parent.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(parent.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(parent.z)));
            float span = Mathf.Max(0.1f, apex.y - m_FloorY), reach = Mathf.Max(0.1f, Profile.HalfLength);
            s_Verts.Clear();
            s_Uvs.Clear();
            s_Tris.Clear();
            s_Verts.Add(Vector3.zero);
            s_Uvs.Add(new Vector2(0f, 1f));
            for (int i = 0; i < half.Length; i++)
            {
                s_Verts.Add(new Vector3(half[i].x, half[i].y, 0f));
                s_Uvs.Add(new Vector2(half[i].x / reach, 1f + half[i].y / span)); // as the old triangle: v 1 at the lens, 0 on the floor
                if (i > 0) { s_Tris.Add(0); s_Tris.Add(i + 1); s_Tris.Add(i); }
            }
            mesh.Clear();
            mesh.SetVertices(s_Verts);
            mesh.SetUVs(0, s_Uvs);
            mesh.SetTriangles(s_Tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (m_MeshA != null) Destroy(m_MeshA);
            if (m_MeshB != null) Destroy(m_MeshB);
        }

        /// <summary>
        /// Bounces the body around the ceiling and spins it. The two halves of the light are its children: they are put back
        /// where this frame's rays were cast, so the half cut by a wall stays on the wall's side.
        /// </summary>
        public void Drift(float dt)
        {
            var p = transform.position + m_Dir * Profile.Speed * dt;
            if (p.x < m_Min.x || p.x > m_Max.x) { m_Dir.x = -m_Dir.x; p.x = Mathf.Clamp(p.x, m_Min.x, m_Max.x); }
            if (p.z < m_Min.z || p.z > m_Max.z) { m_Dir.z = -m_Dir.z; p.z = Mathf.Clamp(p.z, m_Min.z, m_Max.z); }
            p.y = CeilingY;
            m_Yaw = Mathf.Repeat(m_Yaw + 40f * dt, 360f);
            transform.SetPositionAndRotation(p, Quaternion.Euler(180f, m_Yaw, 0f));
            if (m_RayDir.sqrMagnitude < 0.5f) return;
            if (FanA != null) FanA.SetPositionAndRotation(m_RayApex, Quaternion.LookRotation(Vector3.Cross(m_RayDir, Vector3.up), Vector3.up));
            if (FanB != null) FanB.SetPositionAndRotation(m_RayApex, Quaternion.LookRotation(Vector3.Cross(-m_RayDir, Vector3.up), Vector3.up));
        }

        protected override void TickServer()
        {
            Drift(Time.deltaTime);

            // the rays cast this frame by TickLocal (it runs first on every peer, the server too)
            foreach (var pl in PlayerController.All)
            {
                if (pl.IsDown || LevelLayout.FloorOf(pl.Position.y) != LevelLayout.FloorOf(m_FloorY + 0.1f)) continue;
                if (ScanLine.FanTouches(m_RayApex, m_RayDir, m_HalfA, m_HalfB, pl.Position, pl.Height)) { Trip(pl); return; }
            }
            Forget();
        }
    }
}
