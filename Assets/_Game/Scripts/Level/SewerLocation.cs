using System;
using System.Collections.Generic;
using Stealth.Game;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The sewer between two houses (prefab Level/Sewer), assembled on every peer from the kit pieces by a
    /// <see cref="SewerPlan"/> (the same on every peer: the run's seed and the depth): a closed end, the stretch where
    /// the team's car waits under the hatch it came in through, a gate (opened with a lever on the far side), the stretch
    /// the house's exit drops into, and the hall where three tunnels lead on to the next houses (<see cref="Branches"/>:
    /// left, middle, right). The team drives the car into a tunnel to choose it; tunnels that lead nowhere are barred.
    /// The road is a steel grate over the water. Nothing in it is networked except the car. If the run ends here, the
    /// water rises (<see cref="Flood"/>).
    /// </summary>
    public class SewerLocation : MonoBehaviour
    {
        public static SewerLocation Active { get; private set; }

        [Serializable]
        public class Branch
        {
            [Tooltip("The mouth of the tunnel: +X leads into it.")]
            public Transform Mouth;
            [Tooltip("Bars that close the tunnel when it leads nowhere.")]
            public GameObject Grate;
            [Tooltip("The panel over the mouth: where it leads.")]
            public Stealth.UI.RouteSignView Sign;
            [Tooltip("Lamps inside and the zone on the floor: shown only when the tunnel is open.")]
            public GameObject Lamp;
            [Tooltip("Bricks in the mouth when it leads nowhere (the bars stay behind them as the stop).")]
            public GameObject Plug;
        }

        [Header("Kit (Tools/Blender/sewer.py)")]
        [Tooltip("Straight 6 m segments, one per variant.")]
        public GameObject[] Segments;
        public GameObject Cap, Hub, Lamp, Barrel, Crate, Ladder, Manhole, Flooding;
        [Tooltip("The segment with the alcove beside the car: a ladder up a shaft to the lid of the house's floor (shut from below here).")]
        public GameObject Alcove;
        [Tooltip("Bricks that fill a mouth of the hall leading nowhere this time.")]
        public GameObject Plug;
        [Tooltip("An invisible wall across the tunnel behind the car: the way back (where the car came from) is seen, not walked.")]
        public GameObject BackWall;

        /// <summary>How many segments of the way back are seen behind the car (past the invisible wall), then the cap.</summary>
        public const int BackSegments = 7;
        [Tooltip("The gate across the tunnel: frame, shutter, lever (SewerGate).")]
        public SewerGate GatePrefab;
        [Tooltip("A tunnel leading on with its bars, the lights and the zone inside, the stop wall (SewerBranch).")]
        public SewerBranch BranchPrefab;
        public Stealth.UI.RouteSignView SignPrefab;
        /// <summary>How far in front of the tunnels' mouths the route panels hang: the end wall of the hall is 0.6 m of brick.</summary>
        public const float SignOffWall = 0.75f;

        // built at run time by Build (not in the prefab)
        [NonSerialized] public Transform Water;
        [NonSerialized] public Branch[] Branches = Array.Empty<Branch>();
        [NonSerialized] public SewerGate Gate;
        /// <summary>Where the team stands when it comes down, and where the car waits (world).</summary>
        public Pose[] Spawns = Array.Empty<Pose>();
        public Pose CarSpot;

        /// <summary>The car inside a tunnel this far past its mouth (and up to this deep) chooses it.</summary>
        public const float VoteFrom = 3.0f, VoteTo = 90f, VoteHalfWidth = 2.2f;
        /// <summary>The invisible wall in every tunnel, metres past its mouth (the tunnel itself goes on into the dark): far
        /// enough to drive off into the tunnel while the departure counts down.</summary>
        public const float StopAt = 95f;

        public SewerPlan Plan { get; private set; }

        float m_WaterBase;
        float m_FloodStart = -1f;
        int m_Ways = -1;

        void Awake() => Active = this;

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // ------------------------------------------------------------------ building

        GameObject Put(GameObject prefab, string name, Vector3 at, float yaw = 0f, Transform parent = null)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, parent != null ? parent : transform);
            go.name = name;
            go.transform.SetLocalPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>Assembles the sewer of this plan (every peer, the same plan → the same sewer).</summary>
        public void Build(SewerPlan plan)
        {
            Plan = plan;
            // the way the car came from: a long tunnel behind it, dimly lit, closed by an invisible wall where the sewer starts
            Put(BackWall, "BackWall", Vector3.zero);
            for (int i = 1; i <= BackSegments; i++)
            {
                float bx = -i * SewerPlan.SegmentLength;
                if (Segments != null && Segments.Length > 0) Put(Segments[(i * 3 + plan.Segments.Count) % Segments.Length], $"BackSegment{i}", new Vector3(bx, 0f, 0f));
                if (i % 2 == 1 && i < BackSegments)
                {
                    int side = i % 4 == 1 ? 1 : -1;
                    var lamp = Put(Lamp, $"BackLamp{i}", new Vector3(bx + SewerPlan.SegmentLength / 2f, 2.3f, side * 3.55f), side > 0 ? 90f : -90f);
                    if (lamp != null)
                        foreach (var l in lamp.GetComponentsInChildren<Light>())
                        {
                            l.color = plan.LampColor;
                            l.intensity = plan.LampIntensity * 0.45f; // fading into the distance
                            l.range = 11f;
                        }
                }
            }
            Put(Cap, "Cap", new Vector3(-BackSegments * SewerPlan.SegmentLength, 0f, 0f));
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                var s = plan.Segments[i];
                if (s.Alcove && Alcove != null) Put(Alcove, $"Segment{i}", new Vector3(s.X, 0f, 0f)); // its lid shut: no ladder to climb here
                else if (Segments != null && Segments.Length > 0) Put(Segments[s.Variant % Segments.Length], $"Segment{i}", new Vector3(s.X, 0f, 0f));
                if (s.LampSide != 0)
                {
                    var lamp = Put(Lamp, $"Lamp{i}", new Vector3(s.X + SewerPlan.SegmentLength / 2f, 2.3f, s.LampSide * 3.55f), s.LampSide > 0 ? 90f : -90f);
                    if (lamp != null)
                        foreach (var l in lamp.GetComponentsInChildren<Light>())
                        {
                            l.color = plan.LampColor;
                            l.intensity = plan.LampIntensity * (s.LampBroken ? 0.35f : 1f);
                            l.range = 13f;
                        }
                }
                foreach (var (x, side, barrel) in s.Junk)
                    Put(barrel ? Barrel : Crate, "Junk", new Vector3(x, 0.15f, side * (SewerPlan.WalkwayZ + 0.1f)), x * 47f);
            }

            CarSpot = new Pose(transform.TransformPoint(new Vector3(plan.CarX, 0.05f, -0.4f)), transform.rotation * Quaternion.Euler(0f, 90f, 0f)); // facing +X

            if (plan.GateX >= 0f && GatePrefab != null)
            {
                Gate = Instantiate(GatePrefab, transform);
                Gate.name = "Gate";
                Gate.transform.localPosition = new Vector3(plan.GateX, 0f, 0f);
                foreach (var use in Gate.GetComponentsInChildren<Interactable>()) use.Configure(0);
                PlaceLever(Gate);
            }

            // where the team stands when it comes down: at the exit's ladder (after a house) or around the car (the start)
            var spawns = new List<Pose>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = plan.ExitLadderX >= 0f
                    ? new Vector3(plan.ExitLadderX + 0.8f + i * 0.7f, 0.2f, SewerPlan.WalkwayZ - 0.1f - (i % 2) * 0.5f)
                    : new Vector3(plan.CarX - 3.5f + i * 1.6f, 0.1f, 2.4f);
                float yaw = plan.ExitLadderX >= 0f ? -90f : 180f; // towards the gate / the car
                spawns.Add(new Pose(transform.TransformPoint(at), transform.rotation * Quaternion.Euler(0f, yaw, 0f)));
            }
            Spawns = spawns.ToArray();
            if (plan.ExitLadderX >= 0f)
            {
                Put(Ladder, "ExitLadder", new Vector3(plan.ExitLadderX, 0.15f, 3.45f));
                Put(Manhole, "ExitManhole", new Vector3(plan.ExitLadderX, 4.15f, 3.2f));
            }

            // the hall and the three tunnels; lamps on its long walls
            Put(Hub, "Hub", new Vector3(plan.HubX, 0f, 0f));
            foreach (var (x, side) in new[] { (6f, 1), (6f, -1), (14.2f, 1), (14.2f, -1) }) // clear of the side mouths (x 7.7..12.3)
            {
                var lamp = Put(Lamp, "HubLamp", new Vector3(plan.HubX + x, 3.1f, side * 7.9f), side > 0 ? 90f : -90f);
                if (lamp != null) foreach (var l in lamp.GetComponentsInChildren<Light>()) { l.color = plan.LampColor; l.intensity = plan.LampIntensity; l.range = 14f; }
            }
            // the three tunnels at their mouths; the mouths no tunnel takes are bricked up
            for (int m = 0; m < SewerPlan.Mouths.Length; m++)
                if (System.Array.IndexOf(plan.TunnelMouths, m) < 0 && Plug != null)
                {
                    var plug = Put(Plug, $"Plug{m}", new Vector3(plan.HubX, 0f, 0f) + SewerPlan.Mouths[m].position);
                    plug.transform.localRotation = SewerPlan.Mouths[m].rotation;
                }
            Branches = new Branch[SewerBranches.Count];
            for (int i = 0; i < SewerBranches.Count; i++)
            {
                var mouth = SewerPlan.Mouths[plan.TunnelMouths[i]];
                var at = new Vector3(plan.HubX, 0f, 0f) + mouth.position;
                if (BranchPrefab == null) continue;
                var b = Instantiate(BranchPrefab, transform);
                b.name = $"Branch{i}";
                b.transform.localPosition = at; // +X leads into it
                b.transform.localRotation = mouth.rotation;
                Stealth.UI.RouteSignView sign = null;
                if (SignPrefab != null)
                {
                    sign = Instantiate(SignPrefab, transform);
                    sign.name = $"Sign{i}";
                    // over the mouth, 15 cm in front of the hall's end wall (its brick is 0.6 m thick: x −0.6..0 from the mouth),
                    // its face to the hall (a world-space canvas reads along its +Z: into the tunnel), tipped down a little
                    sign.transform.SetLocalPositionAndRotation(at + mouth.rotation * new Vector3(-SignOffWall, 4.4f, 0f), mouth.rotation * Quaternion.Euler(-12f, 90f, 0f));
                }
                Branches[i] = new Branch { Mouth = b.transform, Grate = b.Bars, Sign = sign, Lamp = b.WhenOpen, Plug = b.Plug };
            }
            var water = Put(Flooding, "FloodWater", new Vector3(plan.End / 2f, -0.6f, 0f));
            if (water != null)
            {
                water.transform.localScale = new Vector3(plan.End + 40f, 1f, 20f);
                Water = water.transform;
                m_WaterBase = Water.localPosition.y;
            }
        }

        /// <summary>The lever's height on the wall and the stretch past the gate (exit side, +X) it may hang along.</summary>
        static readonly float[] LeverAlong = { 1.2f, 1.6f, 2.0f, 2.4f, 2.8f, 3.2f, 0.9f };
        const float LeverHeight = 1.25f;

        /// <summary>
        /// Hangs the gate's lever on the real face of the tunnel's wall on the exit's side: the segments differ (ribs at
        /// their joints, niches, pipes), so a fixed spot sank into the brick in some of them and E never reached it. A ray
        /// from the tunnel's axis finds the wall at a few heights; the first stretch where it is flat takes the lever, its
        /// back (the model's origin) on the brick.
        /// </summary>
        /// <summary>How far from the gate's axis the wall the lever hangs on may be, metres.</summary>
        const float LeverMinWall = 3.0f, LeverMaxWall = 6.5f;

        void PlaceLever(SewerGate gate)
        {
            var lever = gate.GetComponentInChildren<GateLever>();
            if (lever == null) return;
            Physics.SyncTransforms();
            var g = gate.transform;
            float side = Mathf.Sign(g.InverseTransformPoint(lever.transform.position).z);
            if (side == 0f) side = 1f;
            float Wall(float x, float y)
            {
                var from = g.TransformPoint(new Vector3(x, y, 0f));
                var dir = g.forward * side;
                var hits = Physics.RaycastAll(from, dir, 8f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                foreach (var h in hits)
                {
                    // the tunnel's own drawn wall only: invisible stops (the car's bounds, a tunnel's bars) are flat too
                    if (h.collider.transform.IsChildOf(g) || h.collider is not MeshCollider || !h.collider.TryGetComponent<MeshRenderer>(out var r) || !r.enabled) continue;
                    if (h.distance < best) best = h.distance;
                }
                // a wall is beyond the walkway (z ±3.1 from the tunnel's axis): anything nearer stands in the tunnel
                return best >= LeverMinWall && best < LeverMaxWall ? best : -1f;
            }
            foreach (float x in LeverAlong)
            {
                float a = Wall(x, LeverHeight - 0.25f), b = Wall(x, LeverHeight), c = Wall(x, LeverHeight + 0.25f);
                float l = Wall(x - 0.2f, LeverHeight), r = Wall(x + 0.2f, LeverHeight);
                if (a < 0f || b < 0f || c < 0f || l < 0f || r < 0f) continue;
                float flat = Mathf.Max(Mathf.Abs(a - b), Mathf.Abs(c - b), Mathf.Abs(l - b), Mathf.Abs(r - b));
                if (flat > 0.03f) continue; // a rib, a niche, a pipe: further along
                lever.transform.localPosition = new Vector3(x, LeverHeight, side * (b - 0.005f));
                if (gate.LeverLight != null)
                {
                    // 0.6 m out of the wall and 0.9 m up, looking down at the handle
                    var at = g.TransformPoint(new Vector3(x, LeverHeight + 0.9f, side * (b - 0.6f)));
                    gate.LeverLight.transform.SetPositionAndRotation(at, Quaternion.LookRotation(lever.transform.position - at));
                }
                return;
            }
            Debug.LogWarning("[Sewer] no flat wall for the gate's lever: left where the prefab has it");
        }

        // ------------------------------------------------------------------ queries

        public Vector3 SpawnFor(int slot) =>
            Spawns.Length > 0 ? Spawns[Mathf.Abs(slot) % Spawns.Length].position : transform.position + Vector3.up;

        public Quaternion SpawnRotation(int slot) =>
            Spawns.Length > 0 ? Spawns[Mathf.Abs(slot) % Spawns.Length].rotation : transform.rotation;

        /// <summary>The tunnel (0 left, 1 middle, 2 right) a point (the car) is in, or −1.</summary>
        public int BranchAt(Vector3 p)
        {
            for (int i = 0; i < Branches.Length; i++)
            {
                var m = Branches[i].Mouth;
                if (m == null) continue;
                var local = m.InverseTransformPoint(p);
                if (local.x >= VoteFrom && local.x <= VoteTo && Mathf.Abs(local.z) <= VoteHalfWidth && Mathf.Abs(local.y) < 2.5f) return i;
            }
            return -1;
        }

        /// <summary>Open as many tunnels as there are ways on (1–3); the others get their bars.</summary>
        public void ShowWays(int ways)
        {
            if (ways == m_Ways) return;
            m_Ways = ways;
            for (int i = 0; i < Branches.Length; i++)
            {
                bool open = SewerBranches.WayOf(i, ways) >= 0;
                var b = Branches[i];
                if (b.Grate != null) b.Grate.SetActive(!open);
                if (b.Plug != null) b.Plug.SetActive(!open); // a way that leads nowhere is bricked up: only the ways on show
                if (b.Sign != null) b.Sign.gameObject.SetActive(open);
                if (b.Lamp != null) b.Lamp.SetActive(open);
            }
        }

        /// <summary>The run ends here: the water rises over the next seconds.</summary>
        public void Flood()
        {
            if (m_FloodStart < 0f) m_FloodStart = Time.time;
        }

        /// <summary>The water has risen (the run ended here): a new run builds the sewer afresh.</summary>
        public bool Flooded => m_FloodStart >= 0f;

        void Update()
        {
            if (Water == null || m_FloodStart < 0f) return;
            float t = Mathf.Clamp01((Time.time - m_FloodStart) / 6f);
            var p = Water.localPosition;
            Water.localPosition = new Vector3(p.x, m_WaterBase + Mathf.SmoothStep(0f, 2.6f, t), p.z);
        }
    }
}
