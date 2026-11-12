using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Visible sensors on a mobile robot: camera eyes, microphones, ground probes, a sniffer snout. The prefab carries
    /// all of them; on spawn only those matching the robot's <see cref="RobotSenses"/> stay, so a player can read what a
    /// robot perceives from its look — and a tweak in the robot's config changes its look too.
    /// Every style gives the robot another body, so the mounts are not fixed: whenever the body's meshes change they are
    /// seated again on its surface (<see cref="MeshRay"/> against the body's triangles) — microphones stand on top of it,
    /// probes press against its front down to the floor, the eye and the snout sit on its face.
    /// </summary>
    public class SensorRig : MonoBehaviour
    {
        public RobotController Robot;
        [Tooltip("The robot's model: the mounts are its children, the rest of its meshes is the body they sit on.")]
        public Transform Model;
        [Tooltip("Camera eyes: shown when the robot sees.")]
        public GameObject[] Eyes;
        [Tooltip("Microphones: shown when the robot hears.")]
        public GameObject[] Microphones;
        [Tooltip("Ground probes: shown when the robot feels footsteps through the floor.")]
        public GameObject[] GroundProbes;
        [Tooltip("Sniffer snout: shown when the robot follows scent trails.")]
        public GameObject[] Noses;

        /// <summary>A mount sinks this far into the surface it found (no hairline gap under it).</summary>
        const float Embed = 0.02f;

        static readonly Dictionary<Mesh, (Vector3[] v, int[] t)> s_Meshes = new();
        readonly List<MeshFilter> m_Body = new();
        readonly Dictionary<GameObject, Vector3> m_Rest = new();
        int m_Signature;
        float m_NextCheck;

        /// <summary>Which mounts a robot with these senses shows (pure).</summary>
        public static (bool eyes, bool ears, bool ground, bool nose) Mounts(RobotSenses s) =>
            (s.HasEyes, s.HearingMultiplier > 0f, s.SeismicRange > 0f, s.SmellRange > 0f);

        void Start()
        {
            if (Robot != null && Robot.Profile != null) Apply(Robot.Senses);
            foreach (var group in new[] { Eyes, Microphones, GroundProbes, Noses })
                if (group != null)
                    foreach (var m in group) if (m != null) m_Rest[m] = m.transform.localPosition;
            Seat();
        }

        public void Apply(RobotSenses senses)
        {
            var (eyes, ears, ground, nose) = Mounts(senses);
            Show(Eyes, eyes);
            Show(Microphones, ears);
            Show(GroundProbes, ground);
            Show(Noses, nose);
        }

        static void Show(GameObject[] parts, bool on)
        {
            if (parts == null) return;
            foreach (var p in parts) if (p != null) p.SetActive(on);
        }

        void LateUpdate()
        {
            if (Time.time < m_NextCheck) return;
            m_NextCheck = Time.time + 0.5f;
            if (Signature() != m_Signature) Seat();
        }

        bool IsMount(Transform t)
        {
            foreach (var group in new[] { Eyes, Microphones, GroundProbes, Noses })
                if (group != null)
                    foreach (var m in group) if (m != null && t.IsChildOf(m.transform)) return true;
            return false;
        }

        /// <summary>Changes when the style swaps the body's meshes.</summary>
        int Signature()
        {
            if (Model == null) return 0;
            int s = 17;
            foreach (var mf in m_Body) if (mf != null && mf.sharedMesh != null) s = s * 31 + mf.sharedMesh.GetHashCode();
            return s;
        }

        /// <summary>Puts every mount on the body as it looks now.</summary>
        void Seat()
        {
            if (Model == null) return;
            m_Body.Clear();
            foreach (var mf in Model.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.isReadable && !IsMount(mf.transform)) m_Body.Add(mf);
            m_Signature = Signature();
            if (m_Body.Count == 0) return;

            var b = BodyBounds();
            float above = b.max.y + 0.5f, front = b.max.z + 0.5f;
            if (Microphones != null)
                foreach (var m in Microphones)
                {
                    if (m == null || !m_Rest.TryGetValue(m, out var rest)) continue;
                    // standing on top of the body, as far out to its side as the body reaches
                    foreach (float k in new[] { 1f, 0.75f, 0.5f, 0.3f, 0.12f })
                        if (Cast(new Vector3(rest.x * k, above, rest.z), Vector3.down, out var hit))
                        {
                            m.transform.localPosition = hit + Vector3.down * Embed;
                            break;
                        }
                }
            if (GroundProbes != null)
                foreach (var m in GroundProbes)
                {
                    if (m == null || !m_Rest.TryGetValue(m, out var rest)) continue;
                    // on the floor, pressed against the body's front at the probe's height
                    bool seated = false;
                    foreach (float k in new[] { 1f, 0.75f, 0.5f, 0.3f })
                    {
                        foreach (float y in new[] { 0.25f, 0.4f, 0.6f, 0.8f })
                            if (Cast(new Vector3(rest.x * k, y, front), Vector3.back, out var hit))
                            {
                                m.transform.localPosition = new Vector3(hit.x, rest.y, hit.z + 0.05f);
                                seated = true;
                                break;
                            }
                        if (seated) break;
                    }
                }
            SeatFront(Eyes, 0f);
            SeatFront(Noses, 0f);

            void SeatFront(GameObject[] group, float lift)
            {
                if (group == null) return;
                foreach (var m in group)
                {
                    if (m == null || !m_Rest.TryGetValue(m, out var rest)) continue;
                    if (Cast(new Vector3(rest.x, rest.y + lift, front), Vector3.back, out var hit))
                        m.transform.localPosition = hit + Vector3.back * Embed;
                }
            }
        }

        /// <summary>The body's bounds in the model's space.</summary>
        Bounds BodyBounds()
        {
            bool first = true;
            var bounds = new Bounds();
            foreach (var mf in m_Body)
            {
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = Model.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        /// <summary>The ray (model space) against every body mesh: the nearest hit point, in model space.</summary>
        bool Cast(Vector3 origin, Vector3 dir, out Vector3 hit)
        {
            hit = default;
            float best = float.MaxValue;
            var worldO = Model.TransformPoint(origin);
            var worldD = Model.TransformVector(dir);
            foreach (var mf in m_Body)
            {
                var mesh = mf.sharedMesh;
                if (!s_Meshes.TryGetValue(mesh, out var data)) s_Meshes[mesh] = data = (mesh.vertices, mesh.triangles);
                var o = mf.transform.InverseTransformPoint(worldO);
                var d = mf.transform.InverseTransformVector(worldD);
                if (!MeshRay.Cast(o, d, data.v, data.t, out float t) || t >= best) continue;
                best = t; // t is shared: the same world ray, only expressed in each mesh's space
                hit = Model.InverseTransformPoint(worldO + worldD * t);
            }
            return best < float.MaxValue;
        }
    }
}
