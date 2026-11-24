using System.Collections.Generic;
using Stealth.Audio;
using Stealth.Game;
using Stealth.Items;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The shredder on the car's hood, seen (every peer, by the server's clock: <see cref="Shredder"/>), like a twin-shaft
    /// shredder: the very things thrown in — at their own size — land on a heap in the hopper's mouth, the ones at the bottom
    /// are grabbed by the shafts (up to <see cref="Shredder.Lanes"/> at once): they shudder, tip over and are pulled down
    /// into the funnel while chips of their colour fly up, and the heap settles down onto the shafts as they free up. A
    /// crunch as the shafts take a thing, a burst of fragments and a gulp when it is gone and its fuel is in the tank; the
    /// shafts spin fast meanwhile (<see cref="Car.Grinding"/>). Each thing goes in smallest face down — its longest side
    /// upright, leaning a little (<see cref="Shredder.FeedTurn"/>, by its box measured off its own model) — and the shafts
    /// pull it down end first, its whole length into the funnel.
    /// </summary>
    public class CarShredder : MonoBehaviour
    {
        [Tooltip("The hopper's mouth: the heap sits on it, the shafts are under it.")]
        public Transform Mouth;
        [Tooltip("A thing's look (the bed's piece prefab: mesh, colour or a tool's own model).")]
        public CargoPiece PiecePrefab;
        public ItemVisuals Visuals;
        [Tooltip("Chips and fragments flying out of the mouth (emitted by hand, each in the colour of what is torn).")]
        public ParticleSystem Chips;
        public Car Car;

        /// <summary>How deep the shafts pull a thing below the mouth before it is gone, metres (out of sight in the funnel).</summary>
        const float PullDepth = 0.6f;
        /// <summary>Where on the mouth the shafts take things: the bottom of the heap (mouth-local, metres).</summary>
        static readonly Vector3[] LaneSpots = { new(-0.1f, 0.02f, 0.04f), new(0.1f, 0.02f, -0.02f), new(0f, 0.02f, -0.1f) };

        class Shown
        {
            public CargoPiece Piece;
            public byte Shape, Color;
            public double Start;
            public Vector3 Pos;          // mouth-local, eased towards its place (of its box's centre)
            public Quaternion Spin;      // how it goes in: smallest face down (Shredder.FeedTurn)
            public Vector3 Centre;       // its box's centre in its own axes (the model's origin is not always there)
            public float Half, Height;   // half its height as it goes in, and its whole height
            public bool Taken;           // the shafts have it (crunch played)
            public Color Tint;
        }

        readonly List<Shown> m_Shown = new();
        readonly Stack<CargoPiece> m_Spare = new();
        int m_LastVersion = -1;
        float m_ChipDebt;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || Mouth == null || PiecePrefab == null) return;
            var shredder = gm.Shredder;
            if (shredder.Version != m_LastVersion) Sync(shredder);
            double now = Session.ServerTime;
            if (Car != null) Car.Grinding = shredder.Busy;
            float dt = Time.deltaTime;

            // the heap: waiting things in the order thrown in, layered over the shafts; the taken ones on their lanes
            int waiting = 0, lane = 0, chewing = 0;
            for (int i = 0; i < m_Shown.Count; i++)
            {
                var s = m_Shown[i];
                float progress = Shredder.Progress(new Shredder.Piece { Start = s.Start }, now);
                var t = s.Piece.transform;
                Vector3 place;
                Quaternion turn;
                if (progress >= 0f)
                {
                    chewing++;
                    if (!s.Taken)
                    {
                        s.Taken = true;
                        Sfx.At(Sfx.Bank?.Grind, Mouth.position, 0.9f, Sfx.Group.Items, 0.9f + 0.05f * (lane % 3));
                    }
                    // grabbed by the teeth: it shudders and is pulled down end first, its whole length out of sight
                    var spot = LaneSpots[lane++ % LaneSpots.Length];
                    float pull = Mathf.SmoothStep(0f, 1f, progress);
                    var shake = new Vector3(Mathf.Sin(Time.time * 47f + i), Mathf.Sin(Time.time * 61f + i * 1.7f), Mathf.Sin(Time.time * 53f + i * 2.3f)) * 0.02f * (1f - progress * 0.5f);
                    place = spot + Vector3.up * s.Half + Vector3.down * (pull * (PullDepth + s.Height)) + shake;
                    turn = Quaternion.Euler(Mathf.Sin(Time.time * 37f + i) * 4f, 0f, Mathf.Sin(Time.time * 29f + i) * 4f) * s.Spin;
                    s.Pos = Vector3.Lerp(s.Pos, place, 1f - Mathf.Exp(-14f * dt));
                    s.Piece.gameObject.SetActive(progress < 0.98f);
                }
                else
                {
                    // waiting on the heap: layers over the shafts, settling down as the things under it go
                    int k = waiting++;
                    float a = k * 2.399f;                       // golden angle: spread round the mouth
                    float r = k == 0 ? 0.05f : 0.16f;
                    // standing on its lower end (its box's centre half its height over where it rests)
                    place = new Vector3(Mathf.Cos(a) * r, 0.1f + 0.11f * (k / 3 + (k % 3) * 0.3f) + s.Half, Mathf.Sin(a) * r);
                    turn = s.Spin;
                    // landing: from above the mouth, it falls onto the heap
                    s.Pos = Vector3.Lerp(s.Pos, place, 1f - Mathf.Exp(-9f * dt));
                    s.Piece.gameObject.SetActive(true);
                }
                t.rotation = Mouth.rotation * turn;
                t.position = Mouth.TransformPoint(s.Pos) - t.rotation * s.Centre;
            }

            // chips: by hand, in the colour of what is being torn
            if (Chips != null && chewing > 0)
            {
                m_ChipDebt += dt * 45f * chewing;
                var emit = new ParticleSystem.EmitParams();
                while (m_ChipDebt >= 1f)
                {
                    m_ChipDebt -= 1f;
                    var s = PickChewing(now);
                    if (s == null) break;
                    emit.startColor = Vary(s.Tint);
                    Chips.Emit(emit, 1);
                }
            }
            else m_ChipDebt = 0f;
        }

        Shown PickChewing(double now)
        {
            int n = 0;
            foreach (var s in m_Shown) if (s.Taken) n++;
            if (n == 0) return null;
            int k = (int)(Time.frameCount * 7 + m_ChipDebt * 13) % n;
            foreach (var s in m_Shown)
                if (s.Taken && k-- == 0) return s;
            return null;
        }

        static Color Vary(Color c)
        {
            float v = 0.75f + 0.5f * Mathf.Repeat(Time.time * 17.3f, 1f);
            return new Color(Mathf.Clamp01(c.r * v), Mathf.Clamp01(c.g * v), Mathf.Clamp01(c.b * v), 1f);
        }

        /// <summary>The heap changed: a piece per thing (new ones drop in from above); those gone burst into fragments with a gulp.</summary>
        void Sync(Shredder shredder)
        {
            m_LastVersion = shredder.Version;
            var pile = shredder.Pile;
            var keep = new bool[m_Shown.Count];
            var next = new List<Shown>(pile.Count);
            int dropped = 0;
            foreach (var p in pile)
            {
                int found = -1;
                for (int i = 0; i < m_Shown.Count; i++)
                    if (!keep[i] && m_Shown[i].Shape == p.Shape && m_Shown[i].Color == p.Color && System.Math.Abs(m_Shown[i].Start - p.Start) < 0.01) { found = i; break; }
                if (found >= 0)
                {
                    keep[found] = true;
                    next.Add(m_Shown[found]);
                    continue;
                }
                var piece = Take();
                piece.Show(-1, new CargoItem { Shape = p.Shape, Color = p.Color }, Visuals);
                // (the seed by the thing and its start: the same on every peer)
                int seed = p.Shape * 31 + p.Color * 7 + (int)(p.Start * 1000.0 % 100000.0) * 13;
                var box = LocalBox(piece);
                var spin = Shredder.FeedTurn(box.size, seed);
                float half = Shredder.HalfHeight(spin, box.size);
                next.Add(new Shown
                {
                    Piece = piece, Shape = p.Shape, Color = p.Color, Start = p.Start,
                    Pos = new Vector3(0f, 0.9f + 0.15f * dropped++ + half, 0f),
                    Spin = spin, Centre = box.center, Half = half, Height = 2f * half,
                    Tint = TintOf(p.Shape, p.Color),
                });
            }
            for (int i = 0; i < m_Shown.Count; i++)
            {
                if (keep[i]) continue;
                var gone = m_Shown[i];
                if (gone.Taken)
                {
                    // torn up: a burst of fragments of its colour and a gulp
                    if (Chips != null)
                    {
                        var emit = new ParticleSystem.EmitParams();
                        for (int k = 0; k < 18; k++) { emit.startColor = Vary(gone.Tint); emit.startSize = 0.035f + 0.005f * (k % 9); Chips.Emit(emit, 1); }
                    }
                    Sfx.At(Sfx.Bank?.FuelGulp, Mouth.position, 0.8f, Sfx.Group.Items);
                }
                Recycle(gone.Piece);
            }
            m_Shown.Clear();
            m_Shown.AddRange(next);
        }

        /// <summary>
        /// The box of what a piece shows, in its own axes: its tinted mesh, or a tool's own model (the meshes that are drawn).
        /// The shredder feeds a thing by it (<see cref="Shredder.FeedTurn"/>).
        /// </summary>
        static Bounds LocalBox(CargoPiece piece)
        {
            var toPiece = piece.transform.worldToLocalMatrix;
            bool any = false;
            var box = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            foreach (var f in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || !f.gameObject.activeInHierarchy || !f.TryGetComponent<MeshRenderer>(out var r) || !r.enabled || r.forceRenderingOff) continue;
                var m = toPiece * f.transform.localToWorldMatrix;
                var b = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; }
                    else box.Encapsulate(p);
                }
            }
            return box;
        }

        /// <summary>The colour its chips fly in: the loot's colour, a tool's grey.</summary>
        Color TintOf(byte shape, byte color)
        {
            bool model = Visuals != null && Visuals.Models != null && shape < Visuals.Models.Length && Visuals.Models[shape] != null;
            if (model || ItemCatalog.IsTool(shape) || ItemCatalog.Colors.Length == 0) return new Color(0.55f, 0.55f, 0.53f);
            return ItemCatalog.Colors[Mathf.Clamp(color, 0, ItemCatalog.Colors.Length - 1)].Color;
        }

        CargoPiece Take()
        {
            var p = m_Spare.Count > 0 ? m_Spare.Pop() : Instantiate(PiecePrefab, transform);
            p.gameObject.SetActive(true);
            p.transform.localScale = Vector3.one; // at its own size
            foreach (var c in p.GetComponentsInChildren<Collider>(true)) c.enabled = false; // only to look at: not picked up from the hopper
            return p;
        }

        void Recycle(CargoPiece p)
        {
            p.gameObject.SetActive(false);
            m_Spare.Push(p);
        }
    }
}
