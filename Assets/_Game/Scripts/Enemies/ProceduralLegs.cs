using System;
using Stealth.Configs;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Legs that walk (guard, hound, the wall spider): every foot is planted on the surface under it (the floor, or the
    /// wall the spider crawls on — "down" is always the robot's own −up) and stays there while the body moves; once it
    /// falls behind it steps to its new spot in an arc, the legs in two alternating groups. Knees bend by two-bone
    /// inverse kinematics (<see cref="LegMath"/>). The rig — hip and knee pivots with the thigh and shin meshes — is baked
    /// into the prefab by the asset builder (<see cref="Bake"/>) from parts named AccentLeg{n}Upper / AccentLeg{n}Lower.
    /// Purely visual, runs on every peer after <see cref="RobotAnimator"/> has posed the body.
    /// </summary>
    public class ProceduralLegs : MonoBehaviour
    {
        public const string UpperSuffix = "Upper", LowerSuffix = "Lower";

        [Serializable]
        public class Leg
        {
            public Transform Hip, Knee;
            [Tooltip("Where the foot rests, in the robot's space.")]
            public Vector3 RestFoot;
            [Tooltip("The foot point in the knee pivot's space.")]
            public Vector3 FootInKnee;
            [Tooltip("Which way the knee bends, in the robot's space.")]
            public Vector3 KneeHint;
            public float Upper, Lower;
            [Tooltip("Legs of one group step together, the other group stays planted.")]
            public int Group;

            [NonSerialized] public Quaternion HipRest, KneeRest;
            [NonSerialized] public Vector3 Planted, From, To;
            [NonSerialized] public float StepT;
            [NonSerialized] public bool Stepping, Ready;
        }

        public Leg[] Legs = Array.Empty<Leg>();

        AnimationSettings m_S = new();
        Vector3 m_LastPos, m_Velocity;
        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        void Awake()
        {
            var detector = GetComponent<Detector>();
            if (detector != null && detector.ProfileBase != null) m_S = detector.ProfileBase.Animation;
            foreach (var leg in Legs)
            {
                if (leg.Hip == null || leg.Knee == null) continue;
                leg.HipRest = leg.Hip.localRotation;
                leg.KneeRest = leg.Knee.localRotation;
            }
            m_LastPos = transform.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var move = transform.position - m_LastPos;
            m_LastPos = transform.position;
            bool teleport = move.magnitude > 3f;
            if (!teleport) m_Velocity = Vector3.Lerp(m_Velocity, move / dt, 1f - Mathf.Exp(-8f * dt));
            var up = transform.up;
            var lead = Vector3.ProjectOnPlane(m_Velocity, up) * m_S.StepTime * 0.6f; // step a little ahead of the body

            foreach (var leg in Legs)
            {
                if (leg.Hip == null || leg.Knee == null) continue;
                float length = leg.Upper + leg.Lower;
                var rest = transform.TransformPoint(leg.RestFoot);
                var target = Ground(rest, up, length) + lead;
                if (!leg.Ready || teleport)
                {
                    leg.Planted = target;
                    leg.Stepping = false;
                    leg.Ready = true;
                }
                if (!leg.Stepping && LegMath.ShouldStep(leg.Planted, target, length * m_S.StepStride) && !OtherGroupStepping(leg.Group))
                {
                    leg.Stepping = true;
                    leg.StepT = 0f;
                    leg.From = leg.Planted;
                }
                Vector3 foot;
                if (leg.Stepping)
                {
                    leg.To = target;
                    leg.StepT += dt / Mathf.Max(0.05f, m_S.StepTime);
                    foot = LegMath.Swing(leg.From, leg.To, leg.StepT, up, length * m_S.StepLift);
                    if (leg.StepT >= 1f)
                    {
                        leg.Stepping = false;
                        leg.Planted = leg.To;
                    }
                }
                else foot = leg.Planted;
                Solve(leg, foot);
            }
        }

        bool OtherGroupStepping(int group)
        {
            foreach (var l in Legs)
                if (l.Stepping && l.Group != group) return true;
            return false;
        }

        /// <summary>The surface under the foot's rest spot along the robot's −up (its own colliders ignored), or the spot itself.</summary>
        Vector3 Ground(Vector3 rest, Vector3 up, float length)
        {
            // every layer: a spider on the top floor's ceiling plants its feet on the roof (kept out of vision checks)
            int n = Physics.RaycastNonAlloc(rest + up * length, -up, s_Hits, length * 2f, Perception.SightMask | (1 << LevelMetrics.CeilingLayer), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            var point = rest;
            for (int i = 0; i < n; i++)
            {
                var h = s_Hits[i];
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
                best = h.distance;
                point = h.point;
            }
            return point;
        }

        void Solve(Leg leg, Vector3 foot)
        {
            leg.Hip.localRotation = leg.HipRest;
            leg.Knee.localRotation = leg.KneeRest;
            var hip = leg.Hip.position;
            var knee = LegMath.Knee(hip, foot, leg.Upper, leg.Lower, transform.TransformDirection(leg.KneeHint));
            leg.Hip.rotation = Quaternion.FromToRotation(leg.Knee.position - hip, knee - hip) * leg.Hip.rotation;
            var kneeAt = leg.Knee.position;
            var footNow = leg.Knee.TransformPoint(leg.FootInKnee);
            leg.Knee.rotation = Quaternion.FromToRotation(footNow - kneeAt, foot - kneeAt) * leg.Knee.rotation;
        }

        // ------------------------------------------------------------------ baking (asset builder)

        /// <summary>
        /// Editor / builder: for every AccentLeg{n}Upper / AccentLeg{n}Lower pair under <paramref name="model"/> inserts a
        /// hip pivot (with the thigh) and a knee pivot under it (with the shin) and fills <see cref="Legs"/>. The knee is
        /// the top of the shin, the foot its bottom, the hip the end of the thigh away from the knee. The robot must stand
        /// at the origin, unrotated. Returns false if the model has no such legs.
        /// </summary>
        public static bool Bake(Transform root, Transform model, ProceduralLegs legs)
        {
            var list = new System.Collections.Generic.List<Leg>();
            foreach (var upper in model.GetComponentsInChildren<Transform>(true))
            {
                if (!upper.name.StartsWith("AccentLeg") || !upper.name.EndsWith(UpperSuffix)) continue;
                string baseName = upper.name.Substring(0, upper.name.Length - UpperSuffix.Length);
                Transform lower = null;
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == baseName + LowerSuffix) lower = t;
                var ur = upper.GetComponent<Renderer>();
                var lr = lower != null ? lower.GetComponent<Renderer>() : null;
                if (ur == null || lr == null) continue;
                // Knee = top of the shin, foot = its bottom, hip = the end of the thigh away from the knee — from the
                // vertices, so slanted shins (the spider's) work as well as upright ones.
                var lv = WorldVertices(lower);
                var uv = WorldVertices(upper);
                // Which way the knee bends: from the bounds (the foot pushes a straight shin's middle forward — a biped's
                // knees bend forwards; a spider's raised knee is above its foot, away from the hip).
                var lbb = lr.bounds;
                var boundsKnee = new Vector3(lbb.center.x, lbb.max.y, lbb.center.z);
                var boundsFoot = new Vector3(lbb.center.x, lbb.min.y, lbb.center.z);
                var bend = boundsKnee - (FarEnd(ur.bounds, boundsKnee) + boundsFoot) / 2f;
                Vector3 knee, foot, hip;
                if (lv != null && uv != null)
                {
                    knee = Extreme(lv, Vector3.up);
                    foot = Extreme(lv, Vector3.down);
                    var centre = Vector3.zero;
                    foreach (var v in uv) centre += v;
                    centre /= uv.Length;
                    var away = centre - knee;
                    hip = away.sqrMagnitude > 1e-6f ? Extreme(uv, away.normalized) : FarEnd(ur.bounds, knee);
                }
                else
                {
                    var lb = lr.bounds;
                    knee = new Vector3(lb.center.x, lb.max.y, lb.center.z);
                    foot = new Vector3(lb.center.x, lb.min.y, lb.center.z);
                    hip = FarEnd(ur.bounds, knee);
                }

                var hipPivot = new GameObject(upper.name + RobotAnimator.PivotSuffix).transform;
                hipPivot.SetParent(upper.parent, false);
                hipPivot.position = hip;
                hipPivot.rotation = upper.parent.rotation;
                var kneePivot = new GameObject(lower.name + RobotAnimator.PivotSuffix).transform;
                kneePivot.SetParent(hipPivot, false);
                kneePivot.position = knee;
                kneePivot.rotation = hipPivot.rotation;
                upper.SetParent(hipPivot, true);
                lower.SetParent(kneePivot, true);

                list.Add(new Leg
                {
                    Hip = hipPivot,
                    Knee = kneePivot,
                    RestFoot = root.InverseTransformPoint(foot),
                    FootInKnee = kneePivot.InverseTransformPoint(foot),
                    KneeHint = root.InverseTransformDirection(bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.forward),
                    Upper = Vector3.Distance(hip, knee),
                    Lower = Vector3.Distance(knee, foot),
                });
            }
            // Gait: alternate along each side and between the sides (left front with right back…).
            list.Sort((a, b) => a.RestFoot.z.CompareTo(b.RestFoot.z));
            int left = 0, right = 0;
            foreach (var leg in list)
                leg.Group = leg.RestFoot.x < 0f ? left++ % 2 : (right++ + 1) % 2;
            legs.Legs = list.ToArray();
            return list.Count > 0;
        }

        static Vector3[] WorldVertices(Transform part)
        {
            var mf = part.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return null;
            var verts = mf.sharedMesh.vertices;
            if (verts.Length == 0) return null;
            for (int i = 0; i < verts.Length; i++) verts[i] = part.TransformPoint(verts[i]);
            return verts;
        }

        /// <summary>The middle of the vertices furthest along <paramref name="dir"/> (within 2 cm of the furthest one).</summary>
        public static Vector3 Extreme(Vector3[] verts, Vector3 dir)
        {
            float max = float.MinValue;
            foreach (var v in verts) max = Mathf.Max(max, Vector3.Dot(v, dir));
            var sum = Vector3.zero;
            int n = 0;
            foreach (var v in verts)
            {
                if (Vector3.Dot(v, dir) < max - 0.02f) continue;
                sum += v;
                n++;
            }
            return sum / n;
        }

        /// <summary>The middle of the face of <paramref name="b"/> along its longest side that is further from <paramref name="from"/>.</summary>
        static Vector3 FarEnd(Bounds b, Vector3 from)
        {
            var e = b.extents;
            var axis = e.x >= e.y && e.x >= e.z ? Vector3.right * e.x : e.y >= e.z ? Vector3.up * e.y : Vector3.forward * e.z;
            var a = b.center + axis;
            var c = b.center - axis;
            return (a - from).sqrMagnitude > (c - from).sqrMagnitude ? a : c;
        }
    }
}
