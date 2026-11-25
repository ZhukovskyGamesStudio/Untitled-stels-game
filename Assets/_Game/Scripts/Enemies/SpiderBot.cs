using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Wall crawler without eyes: its room is strung with wires like a simple maze (<see cref="SpiderWebs"/>, every peer
    /// strings the same). It runs around the room along the walls (above the doors), now and then over the ceiling to the
    /// opposite wall. A player who touches a wire is found: it scurries along the walls towards them, leaps on them, hits
    /// on landing and crawls back up. Never leaves its room.
    /// </summary>
    public class SpiderBot : SecurityDevice
    {
        /// <summary>No eye to glow (blind: it feels its webs).</summary>
        public override bool GlowEyes => false;
        /// <summary>Armed, tripped or hacked shows on its glowing parts (it neither sees nor hears: no light cast).</summary>
        public override float StateLine => 1f;

        [Tooltip("Tuning (SpiderProfile) — a separate asset next to the prefab.")]
        public SpiderProfile Profile;
        [Tooltip("One wire of its web: a unit length along local Z (scaled to the wire), its pivot at one end.")]
        public GameObject Strand;
        protected override DeviceProfile Device => Profile;

        enum Mode { Crawl, Leap, Return, Cross, Rush }

        readonly List<SpiderWebs.Strand> m_Web = new();
        readonly List<GameObject> m_WebViews = new();
        /// <summary>The wires strung across its room (the same on every peer).</summary>
        public IReadOnlyList<SpiderWebs.Strand> Web => m_Web;

        Mode m_Mode;
        Vector3 m_Min, m_Max;
        float m_FloorY, m_T, m_Dir = 1f, m_ModeStart, m_NextLeap, m_PauseUntil;
        System.Random m_Rng = new(1);
        Vector3 m_From, m_To;
        IIntruder m_Prey;
        float m_NextCross;
        readonly List<(Vector3 pos, Quaternion rot)> m_Path = new();
        int m_Step;

        /// <summary>Gap between the wall face and the spider's underside, metres.</summary>
        const float WallGap = 0.05f;
        /// <summary>How fast the spider turns on its wall (at corners, when it turns back), degrees a second.</summary>
        const float TurnRate = 300f;
        /// <summary>Where a wall meets the ceiling the body passes this far below the corner, and this far out from the wall.</summary>
        const float CornerDrop = 0.3f, CornerOut = 0.3f;

        /// <summary>Height of the spider's feet plane on the ceiling.</summary>
        float CeilingY => m_FloorY + LevelMetrics.WallHeight - WallGap;

        public float Perimeter => 2f * ((m_Max.x - m_Min.x) + (m_Max.z - m_Min.z));

        protected override Color IdleColor => new(0.6f, 1f, 0.3f);

        protected override void OnDeviceSpawned()
        {
            var layout = ActiveLevel.Layout;
            var rect = layout != null ? layout.RoomRectAt(transform.position) : new Rect(transform.position.x - 5f, transform.position.z - 5f, 10f, 10f);
            StringWeb(layout, rect);
            if (!IsServer) return;
            // Hugging the wall: the body a few centimetres off its face, the feet planted on it (ProceduralLegs).
            float inset = LevelMetrics.WallThickness / 2f + WallGap;
            m_FloorY = layout != null ? LevelLayout.FloorY(LevelLayout.FloorOf(transform.position.y)) : transform.position.y;
            m_Min = new Vector3(rect.xMin + inset, 0, rect.yMin + inset);
            m_Max = new Vector3(rect.xMax - inset, 0, rect.yMax - inset);
            m_Rng = LevelRandom.Next(RandomChannel.Devices);
            m_T = m_Rng.Value() * Perimeter;
            m_NextCross = Time.time + Profile.CeilingEvery * m_Rng.Range(0.3f, 1f);
            var (p, rot) = OnWall(m_T, m_Dir);
            transform.SetPositionAndRotation(p, rot);
        }

        /// <summary>Every peer: the wires of its room, from the room and the house (not from the spider's moves), drawn from the prefab.</summary>
        void StringWeb(LevelLayout layout, Rect rect)
        {
            foreach (var v in m_WebViews) if (v != null) Destroy(v);
            m_WebViews.Clear();
            m_Web.Clear();
            if (layout == null) return;
            float floorY = LevelLayout.FloorY(LevelLayout.FloorOf(transform.position.y));
            var doors = new List<Vector3>();
            foreach (var d in layout.Doorways) doors.Add(d.Position);
            var rng = new System.Random(LevelRandom.Mix(layout.Seed, Salt.SpiderWebs, Mathf.RoundToInt(rect.x * 4f) * 7919 + Mathf.RoundToInt(rect.y * 4f)));
            m_Web.AddRange(SpiderWebs.Plan(rect, floorY, doors, rng, Profile.StrandSpacing, Profile.StrandGap, Profile.MaxStrands));
            if (Strand == null) return;
            var parent = ActiveLevel.Root != null ? ActiveLevel.Root.transform : null;
            foreach (var w in m_Web)
            {
                // one web over the whole sheet (the unit square of the model stretched to it), one of its variants — a big orb,
                // an orb and a cobweb's tangle, two orbs — the same on every peer
                float length = (w.B - w.A).magnitude;
                if (length < 0.1f) continue;
                var dir = (w.B - w.A) / length;
                var view = Instantiate(Strand, w.A, Quaternion.LookRotation(dir), parent);
                view.transform.localScale = new Vector3(1f, w.Height, length);
                var variants = view.GetComponentsInChildren<MeshRenderer>(true);
                int show = variants.Length > 0 ? rng.Next(variants.Length) : -1;
                for (int v = 0; v < variants.Length; v++) variants[v].enabled = v == show;
                m_WebViews.Add(view);
            }
        }

        void OnDestroy()
        {
            foreach (var v in m_WebViews) if (v != null) Destroy(v);
        }

        /// <summary>The distance along its wall loop nearest to a point (where it goes to leap at someone).</summary>
        float NearestT(Vector3 p)
        {
            float w = m_Max.x - m_Min.x, d = m_Max.z - m_Min.z;
            float x = Mathf.Clamp(p.x, m_Min.x, m_Max.x), z = Mathf.Clamp(p.z, m_Min.z, m_Max.z);
            var options = new[]
            {
                (Mathf.Abs(p.z - m_Min.z), x - m_Min.x),
                (Mathf.Abs(p.x - m_Max.x), w + (z - m_Min.z)),
                (Mathf.Abs(p.z - m_Max.z), w + d + (m_Max.x - x)),
                (Mathf.Abs(p.x - m_Min.x), 2f * w + d + (m_Max.z - z)),
            };
            float best = float.MaxValue, t = m_T;
            foreach (var (dist, at) in options) if (dist < best) { best = dist; t = at; }
            return t;
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
            p.y = m_FloorY + Profile.CrawlHeight;
            return (p, Quaternion.LookRotation(tangent * dir, inward));
        }

        /// <summary>
        /// The way over the ceiling to the opposite wall: up its wall to under the corner, onto the ceiling, straight across,
        /// down the far wall to its crawl height (a polyline with the body's orientation at every point).
        /// </summary>
        void StartCrossing()
        {
            float t1 = m_T + Perimeter * (0.5f + m_Rng.Range(-0.12f, 0.12f));
            var (a, rotA) = OnWall(m_T, m_Dir);
            var (b, rotB) = OnWall(t1, m_Dir);
            Vector3 inA = rotA * Vector3.up, inB = rotB * Vector3.up; // legs to the wall: up = into the room
            var across = b + inB * CornerOut - (a + inA * CornerOut);
            across.y = 0f;
            if (across.sqrMagnitude < 1f) return; // a tiny room: nothing to cross
            var overhead = Quaternion.LookRotation(across.normalized, Vector3.down);
            float under = CeilingY - CornerDrop;
            m_Path.Clear();
            m_Path.Add((new Vector3(a.x, under, a.z), Quaternion.LookRotation(Vector3.up, inA)));
            m_Path.Add((new Vector3(a.x, CeilingY, a.z) + inA * CornerOut, overhead));
            m_Path.Add((new Vector3(b.x, CeilingY, b.z) + inB * CornerOut, overhead));
            m_Path.Add((new Vector3(b.x, under, b.z), Quaternion.LookRotation(Vector3.down, inB)));
            m_Path.Add((b, rotB));
            m_Step = 0;
            m_T = t1; // a leap from up there returns to the far wall
            m_Mode = Mode.Cross;
        }

        protected override void TickServer()
        {
            switch (m_Mode)
            {
                case Mode.Crawl:
                    if (Profile.CeilingEvery > 0f && Time.time > m_NextCross && Time.time > m_PauseUntil)
                    {
                        m_NextCross = Time.time + Profile.CeilingEvery * m_Rng.Range(0.6f, 1.4f);
                        StartCrossing();
                        if (m_Mode == Mode.Cross) break;
                    }
                    if (Time.time > m_PauseUntil)
                    {
                        m_T += Profile.CrawlSpeed * m_Dir * Time.deltaTime;
                        if (m_Rng.Value() < 0.002f) { m_Dir = -m_Dir; m_PauseUntil = Time.time + m_Rng.Range(0.5f, 2f); }
                    }
                    var (p, rot) = OnWall(m_T, m_Dir);
                    transform.SetPositionAndRotation(p, Quaternion.RotateTowards(transform.rotation, rot, TurnRate * Time.deltaTime));
                    Feel();
                    break;

                case Mode.Rush:
                {
                    // scurrying along the walls the short way round to the point nearest to them, then a leap
                    if (m_Prey == null || m_Prey.IsDown) { m_Mode = Mode.Crawl; break; }
                    float goal = NearestT(m_Prey.Position), loop = Perimeter;
                    float delta = Mathf.Repeat(goal - m_T + loop / 2f, loop) - loop / 2f;
                    m_Dir = delta >= 0f ? 1f : -1f;
                    m_T += Mathf.Clamp(delta, -Profile.RushSpeed * Time.deltaTime, Profile.RushSpeed * Time.deltaTime);
                    var (rp, rrot) = OnWall(m_T, m_Dir);
                    transform.SetPositionAndRotation(rp, Quaternion.RotateTowards(transform.rotation, rrot, TurnRate * 2f * Time.deltaTime));
                    if (Vector3.Distance(rp, m_Prey.Position) <= Profile.LeapRange || Mathf.Abs(delta) < 0.2f) StartLeap(m_Prey);
                    break;
                }

                case Mode.Leap:
                {
                    float k = Mathf.Clamp01((Time.time - m_ModeStart) / Profile.LeapTime);
                    // Mild homing: a sidestep at the last moment still works, standing still does not.
                    if (m_Prey != null) m_To = Vector3.MoveTowards(m_To, m_Prey.Position + Vector3.up * 0.2f, Profile.Homing * Time.deltaTime);
                    var pos = Vector3.Lerp(m_From, m_To, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                    transform.position = pos;
                    var flat = m_To - m_From;
                    flat.y = 0;
                    if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                    if (k >= 1f)
                    {
                        if (m_Prey != null && !m_Prey.IsDown && Vector3.Distance(m_Prey.Position, transform.position) < Profile.HitDistance)
                            m_Prey.HitServer(transform.position);
                        m_Mode = Mode.Return;
                        m_ModeStart = Time.time;
                        m_From = transform.position;
                    }
                    break;
                }

                case Mode.Cross:
                {
                    var (target, facing) = m_Path[m_Step];
                    var pos = Vector3.MoveTowards(transform.position, target, Profile.CrawlSpeed * Time.deltaTime);
                    transform.SetPositionAndRotation(pos, Quaternion.RotateTowards(transform.rotation, facing, TurnRate * Time.deltaTime));
                    if ((pos - target).sqrMagnitude < 0.0004f && ++m_Step >= m_Path.Count) m_Mode = Mode.Crawl;
                    Feel();
                    break;
                }

                case Mode.Return:
                {
                    float k = Mathf.Clamp01((Time.time - m_ModeStart) / Profile.ReturnTime);
                    var (wall, wallRot) = OnWall(m_T, m_Dir);
                    transform.SetPositionAndRotation(Vector3.Lerp(m_From, wall, k), Quaternion.Slerp(transform.rotation, wallRot, k));
                    if (k >= 1f) m_Mode = Mode.Crawl;
                    break;
                }
            }
        }

        /// <summary>A player touching a wire of its web: found at once (the device's alarm), it comes for them.</summary>
        void Feel()
        {
            if (IsHacked) return;
            foreach (var p in PlayerController.All)
            {
                if (p.IsDown) continue;
                foreach (var w in m_Web)
                    if (SpiderWebs.Touches(w, p.Position, p.Height)) { Trip(p); return; }
            }
        }

        protected override void OnDetected(IIntruder p)
        {
            if (Time.time < m_NextLeap || m_Mode is Mode.Leap or Mode.Rush) return;
            m_Prey = p;
            // from the ceiling it drops straight on them; on a wall it runs to them first
            if (m_Mode == Mode.Cross) StartLeap(p);
            else m_Mode = Mode.Rush;
        }

        void StartLeap(IIntruder p)
        {
            m_NextLeap = Time.time + Profile.Cooldown + Profile.LeapTime + Profile.ReturnTime;
            m_Prey = p;
            m_Mode = Mode.Leap;
            m_ModeStart = Time.time;
            m_From = transform.position;
            m_To = p.Position + Vector3.up * 0.2f;
        }
    }
}
