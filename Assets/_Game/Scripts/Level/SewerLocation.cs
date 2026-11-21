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
    /// left, middle, right; the hall's shape and where its mouths are come from the plan too: <see cref="SewerHall"/>, built
    /// from the kit's tiles by <see cref="HallKits"/>). The team drives the car into a tunnel to choose it; tunnels that lead nowhere are barred.
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
        public GameObject Cap, Lamp, Barrel, Crate, Ladder, Manhole, Flooding;
        [Tooltip("The segment with the alcove beside the car: a ladder up a shaft to the lid of the house's floor (shut from below here).")]
        public GameObject Alcove;
        [Tooltip("The way to the house's exit hatch (SewerPlan.Passage): a segment with a doorway in its left wall, 1 m of the passage, its turn, the shaft's foot (its ladder: ShaftLadder).")]
        public GameObject DoorSegment, PassageStraight, PassageCorner, PassageEnd;
        [Serializable]
        public class HallKit
        {
            [Tooltip("A cell's groin vault (8 x 8 m), centred on the cell.")]
            public GameObject Vault;
            [Tooltip("A plain wall on a side of a cell (the frame of a side: SewerHall.Frame, +X out of the hall).")]
            public GameObject Wall;
            [Tooltip("A wall with a mouth of the branch's profile: a tunnel or a plug starts at its outer face.")]
            public GameObject Mouth;
            [Tooltip("The wall the sewer's tunnel comes in through, with its walkways and road running through it.")]
            public GameObject Entry;
            [Tooltip("A column at a corner of the cells (half in the wall on the hall's boundary).")]
            public GameObject Column;
            [Tooltip("A stone arch between two cells, column to column (spans its local Z).")]
            public GameObject Arch;
        }

        [Header("The hall (SewerHall; Tools/Blender/sewer.py kit_hall)")]
        [Tooltip("A cell's floor: the grate road over the water, its beams and the bed.")]
        public GameObject HallFloor;
        [Tooltip("The hall's pieces of each height (SewerHall.Springing): low, high.")]
        public HallKit[] HallKits = Array.Empty<HallKit>();
        [Tooltip("Bricks that fill a mouth of the hall leading nowhere this time.")]
        public GameObject Plug;
        [Tooltip("An invisible wall across the tunnel behind the car: the way back (where the car came from) is seen, not walked.")]
        public GameObject BackWall;
        [Tooltip("Where a branch opens into the kit's wide tunnel again: the drive on to the next house (DrivePath.ReducerX).")]
        public GameObject Reducer;
        [Tooltip("The endless tunnel of the drive (DriveTunnel), here after the reducer, in the basement its approach.")]
        public DriveTunnel Tunnel;

        /// <summary>The sewer stands this deep under the house: the next house goes up during the drive while the sewer is still there.</summary>
        public static readonly Vector3 Origin = new(0f, -60f, 0f);

        /// <summary>How many segments of the way back are seen behind the car (past the invisible wall), then the cap.</summary>
        public const int BackSegments = 7;
        [Tooltip("The gate across the tunnel: frame, shutter, lever (SewerGate).")]
        public SewerGate GatePrefab;
        [Tooltip("A tunnel leading on with its bars, the lights and the zone inside, the stop wall (SewerBranch).")]
        public SewerBranch BranchPrefab;
        public Stealth.UI.RouteSignView SignPrefab;
        /// <summary>How far in front of the tunnels' mouths the route panels hang: the end wall of the hall is 0.6 m of brick.</summary>
        public const float SignOffWall = 0.75f;
        /// <summary>Where a tunnel's panel stands, in its mouth's frame (+X into the tunnel): eye level, beside the car's lane.</summary>
        public static readonly Vector3 SignSpot = new(-SignOffWall, 1.75f, 2.2f);

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

        /// <summary>
        /// The sewer of the house the team is in: it goes on from the basement's gate (the basement is its car's stretch, so
        /// those segments are left out) for as long as the house stands — the team goes down the exit hatch and back up as
        /// often as it likes. Not under a house: the sewer at the run's start (<see cref="Origin"/>, alone).
        /// </summary>
        public bool UnderHouse { get; private set; }

        /// <summary>Set at once after it is made, before it is built (nobody may take it for the sewer between houses meanwhile).</summary>
        public void MarkUnderHouse() => UnderHouse = true;

        /// <summary>Built whole (a piece a frame: its spawns and the gate are there only after the last).</summary>
        public bool Built { get; private set; }

        float m_WaterBase;
        float m_FloodStart = -1f;
        int m_Ways = -1;

        void Awake() => Active = this;

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // ------------------------------------------------------------------ the drive on

        int m_DriveBranch = -1;
        DriveTunnel m_Drive;

        /// <summary>A tunnel's frame (its mouth: +X leads into it), or null.</summary>
        public Transform BranchFrame(int branch) => branch >= 0 && branch < Branches.Length ? Branches[branch].Mouth : null;

        /// <summary>
        /// The car drives on down tunnel <paramref name="branch"/> (<see cref="DrivePath"/>): its dark end goes, the reducer
        /// opens it into the kit's tunnel, which runs on round the car at <paramref name="x"/> (along the tunnel).
        /// </summary>
        public void FollowDrive(int branch, float x)
        {
            var mouth = BranchFrame(branch);
            if (mouth == null) return;
            if (m_DriveBranch != branch)
            {
                m_DriveBranch = branch;
                foreach (var t in mouth.GetComponentsInChildren<Transform>(true))
                    if (t.name == "DarkEnd") t.gameObject.SetActive(false);
                Put(Reducer, "Reducer", new Vector3(DrivePath.ReducerX, 0f, 0f), 0f, mouth);
                if (Tunnel != null)
                {
                    m_Drive = Instantiate(Tunnel, mouth);
                    m_Drive.transform.SetLocalPositionAndRotation(new Vector3(DrivePath.TunnelFrom, 0f, 0f), Quaternion.identity);
                    m_Drive.From = 0;
                }
                VisualStyles.Dress(mouth.gameObject);
            }
            if (m_Drive != null) m_Drive.Follow(x - DrivePath.TunnelFrom);
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
            foreach (var _ in BuildSteps(plan)) { }
        }

        /// <summary>The same a piece of the frame at a time (<see cref="FrameBudget"/>): under a house it goes up while the team plays.</summary>
        public IEnumerable<bool> BuildSteps(SewerPlan plan)
        {
            Plan = plan;
            Built = false;
            // the way the car came from: a long tunnel behind it, dimly lit, closed by an invisible wall where the sewer starts
            // (under a house that is the basement's: here only from its gate on)
            if (!UnderHouse) Put(BackWall, "BackWall", Vector3.zero);
            for (int i = 1; i <= BackSegments && !UnderHouse; i++)
            {
                float bx = -i * SewerPlan.SegmentLength;
                if (Segments != null && Segments.Length > 0) Put(Segments[(i * 3 + plan.Segments.Count) % Segments.Length], $"BackSegment{i}", new Vector3(bx, 0f, 0f));
                bool near = plan.StartLights && i <= 3; // the spawn: the tunnel behind the car lit close by, then fading
                if ((i % 2 == 1 || near) && i < BackSegments)
                {
                    int side = near ? (i % 2 == 1 ? 1 : -1) : i % 4 == 1 ? 1 : -1;
                    var lamp = Put(Lamp, $"BackLamp{i}", new Vector3(bx + SewerPlan.SegmentLength / 2f, 2.3f, side * 3.55f), side > 0 ? 90f : -90f);
                    if (lamp != null)
                        foreach (var l in lamp.GetComponentsInChildren<Light>())
                        {
                            l.color = plan.LampColor;
                            l.intensity = plan.LampIntensity * (near ? 0.9f - 0.15f * i : 0.45f); // fading into the distance
                            l.range = 11f;
                        }
                }
            }
            if (!UnderHouse) Put(Cap, "Cap", new Vector3(-BackSegments * SewerPlan.SegmentLength, 0f, 0f));
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                var s = plan.Segments[i];
                if (UnderHouse && s.X < plan.GateX - 0.01f) continue; // the car's stretch: the basement
                if (FrameBudget.Over) yield return true;
                if (s.Alcove && Alcove != null) Put(Alcove, $"Segment{i}", new Vector3(s.X, 0f, 0f)); // its lid shut: no ladder to climb here
                else if (s.Door != 0 && DoorSegment != null) // the doorway to the exit's passage (the model's is in its left wall: turned round for the right)
                    Put(DoorSegment, $"Segment{i}", new Vector3(s.Door > 0 ? s.X : s.X + SewerPlan.SegmentLength, 0f, 0f), s.Door > 0 ? 0f : 180f);
                else if (Segments != null && Segments.Length > 0) Put(Segments[s.Variant % Segments.Length], $"Segment{i}", new Vector3(s.X, 0f, 0f));
                if (s.LampSide != 0)
                {
                    float boost = plan.StartLights ? SewerPlan.StartLampBoost : 1f;
                    // both walls: the second lamp a third of a segment along, so the two pools overlap instead of stacking
                    int lamps = s.LampBoth ? 2 : 1;
                    for (int k = 0; k < lamps; k++)
                    {
                        int side = k == 0 ? s.LampSide : -s.LampSide;
                        float along = s.X + SewerPlan.SegmentLength * (k == 0 ? 0.5f : 0.15f);
                        var lamp = Put(Lamp, k == 0 ? $"Lamp{i}" : $"Lamp{i}b", new Vector3(along, 2.3f, side * 3.55f), side > 0 ? 90f : -90f);
                        if (lamp != null)
                            foreach (var l in lamp.GetComponentsInChildren<Light>())
                            {
                                l.color = plan.LampColor;
                                l.intensity = plan.LampIntensity * boost * (s.LampBroken ? 0.35f : 1f);
                                l.range = plan.StartLights ? 15f : 13f;
                            }
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
            yield return true;

            foreach (var step in BuildPassage(plan)) yield return step;

            // the hall: its cells (floor and vault), walls (plain, the way in, the mouths), columns, arches, lamps
            BuildHall(plan);
            yield return true;
            // the three tunnels at their mouths; the mouths no tunnel takes are bricked up
            var hub = new Vector3(plan.HubX, 0f, 0f);
            for (int m = 0; m < plan.Mouths.Length; m++)
                if (Array.IndexOf(plan.TunnelMouths, m) < 0 && Plug != null)
                    PutAt(Plug, $"Plug{m}", hub + plan.Mouths[m].position, plan.Mouths[m].rotation);
            Branches = new Branch[SewerBranches.Count];
            for (int i = 0; i < SewerBranches.Count; i++)
            {
                if (FrameBudget.Over) yield return true;
                var mouth = plan.Mouths[plan.TunnelMouths[i]];
                var at = hub + mouth.position;
                if (BranchPrefab == null) continue;
                var b = Instantiate(BranchPrefab, transform);
                b.name = $"Branch{i}";
                b.transform.localPosition = at; // +X leads into it
                b.transform.localRotation = mouth.rotation;
                foreach (var light in b.GetComponentsInChildren<Light>(true)) light.intensity *= 0.45f; // the way on lies in the dark
                Stealth.UI.RouteSignView sign = null;
                if (SignPrefab != null)
                {
                    sign = Instantiate(SignPrefab, transform);
                    sign.name = $"Sign{i}";
                    // at eye level in the mouth, on the driver's side (left-hand drive: +Z looking in), clear of the car's lane,
                    // 15 cm in front of the hall's end wall (its brick is 0.6 m thick: x −0.6..0 from the mouth), its face to the
                    // hall (a world-space canvas reads along its +Z: into the tunnel) — over the mouth the car's roof hid it
                    sign.transform.SetLocalPositionAndRotation(at + mouth.rotation * SignSpot, mouth.rotation * Quaternion.Euler(0f, 90f, 0f));
                }
                Branches[i] = new Branch { Mouth = b.transform, Grate = b.Bars, Sign = sign, Lamp = b.WhenOpen, Plug = b.Plug };
            }
            // the water that rises if the run ends here: under the whole sewer and the hall
            float z0 = Mathf.Min(-10f, plan.Hall.MinZ - 2f), z1 = Mathf.Max(10f, plan.Hall.MaxZ + 2f);
            var water = Put(Flooding, "FloodWater", new Vector3(plan.End / 2f, -0.6f, (z0 + z1) / 2f));
            if (water != null)
            {
                water.transform.localScale = new Vector3(plan.End + 40f, 1f, z1 - z0);
                Water = water.transform;
                m_WaterBase = Water.localPosition.y;
            }
            m_Ways = -1; // (the tunnels' bars and signs for the ways on: set again now that they are there)
            Built = true;
        }

        /// <summary>The passage's lamps on its wall every so many metres of a straight stretch.</summary>
        const float PassageLampEvery = 6f;

        /// <summary>
        /// The way from the doorway to the shaft under the house's exit hatch (<see cref="SewerPlan.Passage"/>): its straight
        /// stretches of the kit's 1 m pieces (the last ones stretched a little to fit), its turns, lamps along its wall, and the
        /// shaft's foot, whose ladder climbs to the hatch's lid in the house's floor (<see cref="ShaftLadder"/>, the lid's key
        /// <see cref="OpenKeys.ExitShaft"/>) — up and down like the basement's.
        /// </summary>
        IEnumerable<bool> BuildPassage(SewerPlan plan)
        {
            for (int i = 0; i < plan.Passage.Count; i++)
            {
                if (FrameBudget.Over) yield return true;
                var piece = plan.Passage[i];
                var rot = Quaternion.Euler(0f, piece.Yaw, 0f);
                var dir = rot * Vector3.right;
                switch (piece.Kind)
                {
                    case PassageKind.Straight:
                        int n = Mathf.Max(1, Mathf.RoundToInt(piece.Length));
                        float step = piece.Length / n;
                        for (int k = 0; k < n; k++)
                        {
                            var go = Put(PassageStraight, $"Passage{i}_{k}", piece.Position + dir * (step * k), piece.Yaw);
                            if (go != null) go.transform.localScale = new Vector3(step, 1f, 1f);
                        }
                        var wall = rot * Vector3.forward; // its left wall
                        for (float d = PassageLampEvery / 2f; d < piece.Length; d += PassageLampEvery)
                        {
                            var lamp = Put(Lamp, $"PassageLamp{i}", piece.Position + dir * d + wall * (SewerPlan.PassageHalf - 0.05f) + Vector3.up * 1.9f, SewerPlan.YawOf(-wall));
                            if (lamp != null)
                                foreach (var l in lamp.GetComponentsInChildren<Light>())
                                {
                                    l.color = plan.LampColor;
                                    l.intensity = plan.LampIntensity * 0.5f;
                                    l.range = 7f;
                                }
                        }
                        break;
                    case PassageKind.Corner:
                        Put(PassageCorner, $"PassageCorner{i}", piece.Position, piece.Yaw);
                        break;
                    case PassageKind.End:
                        var end = Put(PassageEnd, "PassageEnd", piece.Position, piece.Yaw);
                        if (end == null || !end.TryGetComponent<ShaftLadder>(out var ladder)) break;
                        var o = end.transform;
                        var up = o.right; // into the shaft, towards its ladder
                        var hatch = transform.TransformPoint(new Vector3(plan.ExitHatch.x, Basement.Depth, plan.ExitHatch.y));
                        var foot = o.position + up * 0.3f + Vector3.up * 0.2f;
                        var column = o.position + up * (2f * SewerPlan.PassageHalf - 0.25f - 0.4f) + Vector3.up * 0.2f; // 0.4 m in front of the rungs
                        var top = new Vector3(column.x, hatch.y + 0.1f, column.z);
                        var off = hatch + up * (Basement.ShaftHalf + 0.55f) + Vector3.up * 0.05f;
                        ladder.Setup(new[] { foot, column, top, off }, hatch, OpenKeys.ExitShaft);
                        break;
                }
            }
        }

        GameObject PutAt(GameObject prefab, string name, Vector3 at, Quaternion rotation)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform);
            go.name = name;
            go.transform.SetLocalPositionAndRotation(at, rotation);
            return go;
        }

        /// <summary>The hall of the plan from the kit's tiles (SewerHall: hall-local from the plan's HubX).</summary>
        void BuildHall(SewerPlan plan)
        {
            var hall = plan.Hall;
            var hub = new Vector3(plan.HubX, 0f, 0f);
            var kit = HallKits != null && HallKits.Length > 0 ? HallKits[Mathf.Clamp(hall.Height, 0, HallKits.Length - 1)] : null;
            foreach (var c in hall.Ordered)
            {
                var at = hub + SewerHall.CellCentre(c);
                Put(HallFloor, $"HallFloor{c.x}_{c.y}", at);
                if (kit != null) Put(kit.Vault, $"HallVault{c.x}_{c.y}", at);
            }
            if (kit == null) return;
            foreach (var e in hall.Edges)
            {
                var piece = e.Kind switch { HallSide.Entry => kit.Entry, HallSide.Mouth => kit.Mouth, _ => kit.Wall };
                PutAt(piece, $"Hall{e.Kind}{e.Cell.x}_{e.Cell.y}_{e.Out.x}_{e.Out.y}", hub + e.Frame.position, e.Frame.rotation);
            }
            foreach (var v in hall.Columns) Put(kit.Column, $"HallColumn{v.x}_{v.y}", hub + SewerHall.Corner(v));
            for (int i = 0; i < hall.Arches.Count; i++) PutAt(kit.Arch, $"HallArch{i}", hub + hall.Arches[i].position, hall.Arches[i].rotation);
            for (int i = 0; i < hall.Lamps.Count; i++)
            {
                var lamp = PutAt(Lamp, $"HallLamp{i}", hub + hall.Lamps[i].position, hall.Lamps[i].rotation);
                if (lamp != null)
                    foreach (var l in lamp.GetComponentsInChildren<Light>())
                    {
                        l.color = plan.LampColor;
                        l.intensity = plan.LampIntensity;
                        l.range = 14f;
                    }
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
