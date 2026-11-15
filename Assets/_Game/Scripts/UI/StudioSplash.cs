using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// The studio's splash at the start of the game (prefab UI/Splash, in the scene over the menu; Unity's own splash is
    /// off): the Zhukovsky Games wing swoops in, opens its eye, watches the name being written beside it, blinks and
    /// flies off; then the navy fades off the menu. Once per launch, not shown to the autopilot. Motion, timings and
    /// colours are those of the studio's approved animation (as in the studio's other games: 4.4 s, with its sound).
    /// </summary>
    public class StudioSplash : MonoBehaviour
    {
        public Image Veil;
        public CanvasGroup Group;
        [Tooltip("The 1920 x 1080 design frame, scaled to fit the screen.")]
        public RectTransform Content;
        [Tooltip("The name's clip (it is written left to right behind its moving edge).")]
        public RectTransform Clip;
        public CanvasGroup WordGroup;
        public RectTransform Wing;
        public Blob Body, Pupil, Glint;
        public AudioSource Sound;

        /// <summary>While it plays the menu's music waits (AudioDirector).</summary>
        public static bool Holding { get; private set; }
        static bool s_Shown;

        static readonly Color Navy = new Color32(0x2A, 0x32, 0x4B, 0xFF), Orange = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        const float S = 2.4f;                      // one unit of the 800x400 animation = 2.4 px of the 1920x1080 design
        const float Duration = 4400f, TExit = 3350f, TFly = 1250f, Rest = -10f, Raise = 2.5f;

        float m_T = -1f, m_Out = -1f, m_Wait;
        int m_Frames;

        void Start()
        {
            bool autopilot = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autotest") >= 0;
            if (s_Shown || autopilot) { gameObject.SetActive(false); return; }
            s_Shown = true;
            Holding = true;
            Veil.color = Navy;
            Body.color = Orange;
            Pupil.color = Navy;
            Glint.color = Orange;
            Body.SetTriangles(Triangulate(Airfoil()));
            if (Sound != null && Sound.clip != null && Sound.clip.loadState != AudioDataLoadState.Loaded) Sound.clip.LoadAudioData();
            Fit();
            Render(0f);
        }

        void OnDisable() => Holding = false;

        /// <summary>The design frame scaled to fit the canvas (letterboxed by the navy).</summary>
        void Fit()
        {
            var size = ((RectTransform)transform).rect.size;
            float k = Mathf.Min(size.x / 1920f, size.y / 1080f);
            if (k > 0f) Content.localScale = Vector3.one * k;
        }

        void Update()
        {
            Fit();
            // the first frames after loading are long: the clock starts once the game runs smoothly
            if (m_Frames++ < 2) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 1000f;
            if (m_T < 0f)
            {
                // the sound and the picture start together: wait a little for the sound to be ready
                m_Wait += dt;
                var c = Sound != null ? Sound.clip : null;
                if (c != null && c.loadState == AudioDataLoadState.Loading && m_Wait < 1500f) return;
                m_T = 0f;
                if (c != null) { Sound.volume = 0.9f * Player.GameSettings.MasterVolume; Sound.Play(); }
            }
            else m_T += dt;
            Render(Mathf.Min(m_T, Duration));
            if (m_T >= Duration && m_Out < 0f) m_Out = 0f;
            if (m_Out < 0f) return;
            // the empty navy fades off the menu
            m_Out += dt;
            float k = Mathf.Clamp01(m_Out / 600f);
            Group.alpha = 1f - k * k * (3f - 2f * k);
            if (k >= 1f) gameObject.SetActive(false);
        }

        // ---------------- the motion (a port of the approved animation; times in ms, units of its 800x400 frame) ----------------
        static float Clamp(float v, float a, float b) => Mathf.Max(a, Mathf.Min(b, v));
        static float Seg(float t, float a, float b) => Clamp((t - a) / (b - a), 0f, 1f);
        static float Back(float t) { const float s = 1.70158f; return 1f + (s + 1f) * Mathf.Pow(t - 1f, 3f) + s * Mathf.Pow(t - 1f, 2f); }
        static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);
        static float EaseOutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);
        static float EaseOutQuart(float t) => 1f - Mathf.Pow(1f - t, 4f);
        static float EaseInCubic(float t) => t * t * t;
        static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

        static readonly Vector2 P0 = new(1080, 70), P1 = new(820, 320), P2 = new(430, 148), P3 = new(190, 200);
        static Vector2 Bez(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 a3, float u) { float a = 1f - u; return a * a * a * a0 + 3f * a * a * u * a1 + 3f * a * u * u * a2 + u * u * u * a3; }
        static Vector2 DBez(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 a3, float u) { float a = 1f - u; return 3f * a * a * (a1 - a0) + 6f * a * u * (a2 - a1) + 3f * u * u * (a3 - a2); }

        void Render(float t)
        {
            // flight: in fast from the top right, a dive, a pull-up into the resting spot
            float p = Seg(t, 0f, TFly), u = EaseOutQuint(p);
            var pos = Bez(P0, P1, P2, P3, u);
            var d = DBez(P0, P1, P2, P3, u);
            float tangent = Mathf.Atan2(-d.y, -d.x) * Mathf.Rad2Deg;
            float level = Seg(t, TFly - 250f, TFly + 150f);
            float rot = tangent * (1f - level) + Rest * level;
            float x = pos.x, y = pos.y;
            float speed = d.magnitude * (1f - p) * (1f - p);
            float sx = 1f + Clamp(speed / 900f, 0f, 1f) * 0.06f;
            if (t > TFly)
            {
                float h = EaseInOutSine(Seg(t, TFly, TFly + 900f));
                y += 2.2f * h * Mathf.Sin((t - TFly) / 430f);
                rot += 0.8f * h * Mathf.Sin((t - TFly) / 520f);
            }
            // exit: a little dip, then off nose-first along an arc
            float ex = Seg(t, TExit + 200f, TExit + 820f), exi = EaseInCubic(ex);
            if (ex > 0f)
            {
                var e0 = new Vector2(x, y); var e1 = new Vector2(x - 70f, y + 46f); var e2 = new Vector2(x - 250f, y - 30f); var e3 = new Vector2(x - 560f, y - 250f);
                var q = Bez(e0, e1, e2, e3, exi);
                var dq = DBez(e0, e1, e2, e3, exi);
                float tang = Mathf.Atan2(-dq.y, -dq.x) * Mathf.Rad2Deg;
                float bl2 = Seg(ex, 0f, 0.35f);
                rot = rot * (1f - bl2) + tang * bl2;
                x = q.x; y = q.y; sx = 1f;
            }
            Wing.anchoredPosition = new Vector2((x - 400f) * S, (200f - y) * S);
            Wing.localEulerAngles = new Vector3(0f, 0f, -rot); // the animation's y points down
            Wing.localScale = new Vector3(sx, 1f, 1f);

            // the eye opens with a little overshoot, glances along the name as it is written, blinks, closes before leaving
            float op = Seg(t, 1150f, 1450f);
            float ry = 14f * Back(op);
            float cyBase = -5f - Raise * (1f - EaseOut(Seg(t, 1450f, 1800f)));
            float w = EaseOutQuart(Seg(t, 1600f, 2300f));
            float toWord = EaseOut(Seg(t, 1500f, 1750f));
            float track = 6f + 5f * w;
            float bl = Seg(t, 2600f, 2800f);
            float after = bl >= 0.5f ? 1f : 0f;
            float dx = track * toWord * (1f - after), dy = 1.5f * toWord * (1f - after) + -3f * after;
            if (bl > 0f && bl < 1f) ry *= bl < 0.5f ? 1f - bl * 2f : (bl - 0.5f) * 2f;
            ry *= 1f - Seg(t, TExit, TExit + 160f);
            Pupil.Ellipse(new Vector2((-24f + dx) * S, -(cyBase + dy) * S), 14f * S, Mathf.Max(0f, ry) * S);
            if (ry > 8f) Glint.Ellipse(new Vector2((-21f + dx * 1.2f) * S, -(cyBase - 3f + dy * 1.2f) * S), 6f * S, 6f * S);
            else Glint.Ellipse(Vector2.zero, 0f, 0f);

            // the name: written left to right after the eye has opened, with a little push; it fades as the wing leaves
            Clip.anchoredPosition = new Vector2((300f - 400f - 12f * (1f - w)) * S, 0f);
            Clip.sizeDelta = new Vector2(430f * w * S, 240f * S);
            WordGroup.alpha = 1f - Seg(t, TExit + 200f, TExit + 600f);
        }

        // ---------------- the wing's outline (the approved airfoil path) ----------------
        const string Path = "100.7,5.3 100.4,5.2 99.9,5.1 99.3,5.1 98.7,4.9 97.9,4.8 97.0,4.6 96.0,4.5 94.9,4.2 93.7,4.0 92.4,3.8 91.0,3.5 89.5,3.2 87.9,2.9 86.2,2.5 84.4,2.2 82.6,1.8 80.7,1.4 78.7,1.0 76.6,0.6 74.5,0.2 72.3,-0.3 70.0,-0.8 67.7,-1.2 65.3,-1.7 62.8,-2.2 60.3,-2.7 57.8,-3.2 55.2,-3.7 52.5,-4.2 49.8,-4.7 47.1,-5.2 44.4,-5.7 41.6,-6.2 38.8,-6.7 35.9,-7.2 33.0,-7.7 30.1,-8.1 27.2,-8.6 24.3,-9.0 21.4,-9.5 18.4,-9.9 15.4,-10.3 12.5,-10.7 9.5,-11.0 6.5,-11.4 3.5,-11.7 0.5,-12.0 -2.4,-12.3 -5.4,-12.5 -8.3,-12.8 -11.3,-13.0 -14.2,-13.2 -17.1,-13.3 -20.0,-13.5 -22.9,-13.6 -25.7,-13.6 -28.5,-13.7 -31.3,-13.7 -34.1,-13.7 -36.8,-13.7 -39.5,-13.6 -42.2,-13.6 -44.8,-13.5 -47.4,-13.3 -49.9,-13.2 -52.4,-13.0 -54.9,-12.8 -57.3,-12.5 -59.6,-12.3 -61.9,-12.0 -64.2,-11.7 -66.4,-11.3 -68.5,-11.0 -70.6,-10.6 -72.7,-10.2 -74.6,-9.7 -76.6,-9.3 -78.4,-8.8 -80.2,-8.4 -81.9,-7.9 -83.6,-7.3 -85.1,-6.8 -86.7,-6.3 -88.1,-5.7 -89.5,-5.1 -90.8,-4.6 -92.0,-4.0 -93.2,-3.4 -94.2,-2.8 -95.2,-2.2 -96.2,-1.5 -97.0,-0.9 -97.8,-0.3 -98.5,0.3 -99.1,0.9 -99.6,1.6 -100.0,2.2 -100.4,2.8 -100.7,3.4 -100.9,4.0 -101.0,4.6 -101.0,5.2 -100.9,5.7 -100.8,6.3 -100.6,6.8 -100.2,7.4 -99.8,7.9 -99.4,8.4 -98.8,8.9 -98.1,9.3 -97.4,9.8 -96.6,10.2 -95.7,10.6 -94.7,11.0 -93.6,11.3 -92.4,11.7 -91.2,12.0 -89.8,12.3 -88.4,12.5 -86.9,12.8 -85.3,13.0 -83.7,13.2 -81.9,13.3 -80.1,13.5 -78.2,13.6 -76.3,13.6 -74.2,13.7 -72.1,13.7 -69.9,13.7 -67.6,13.7 -65.3,13.7 -62.8,13.6 -60.4,13.5 -57.8,13.4 -55.2,13.2 -52.5,13.1 -49.8,12.9 -47.0,12.7 -44.1,12.4 -41.2,12.2 -38.2,11.9 -35.2,11.7 -32.2,11.4 -29.0,11.1 -25.9,10.8 -22.7,10.4 -19.5,10.1 -16.2,9.8 -12.9,9.4 -9.6,9.1 -6.3,8.8 -2.9,8.4 0.5,8.1 3.9,7.7 7.3,7.4 10.7,7.1 14.1,6.8 17.5,6.5 20.9,6.2 24.2,5.9 27.6,5.6 30.9,5.4 34.3,5.1 37.5,4.9 40.8,4.7 44.0,4.5 47.1,4.4 50.2,4.2 53.3,4.1 56.3,4.0 59.2,3.9 62.1,3.9 64.8,3.8 67.6,3.8 70.2,3.8 72.7,3.8 75.2,3.8 77.5,3.8 79.8,3.9 81.9,3.9 84.0,4.0 85.9,4.1 87.8,4.2 89.5,4.3 91.1,4.4 92.6,4.5 94.0,4.6 95.2,4.7 96.3,4.8 97.4,4.9 98.2,5.0 99.0,5.1 99.6,5.1 100.2,5.2 100.6,5.3 100.8,5.3 101.0,5.3";

        /// <summary>The outline in design pixels (y up), without repeated points.</summary>
        static List<Vector2> Airfoil()
        {
            var pts = new List<Vector2>();
            foreach (var pair in Path.Split(' '))
            {
                var xy = pair.Split(',');
                var v = new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture) * S, -float.Parse(xy[1], CultureInfo.InvariantCulture) * S);
                if (pts.Count == 0 || (v - pts[pts.Count - 1]).sqrMagnitude > 0.01f) pts.Add(v);
            }
            if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < 0.01f) pts.RemoveAt(pts.Count - 1);
            return pts;
        }

        /// <summary>Ear clipping: the wing is a thin curved outline, so a simple fan would spill out of it.</summary>
        static List<Vector2> Triangulate(List<Vector2> poly)
        {
            var res = new List<Vector2>();
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            float area = 0f;
            for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; area += a.x * b.y - b.x * a.y; }
            bool ccw = area > 0f;
            int guard = 0;
            while (idx.Count > 3 && guard++ < 100000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    float cr = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (Mathf.Abs(cr) < 1e-4f) { idx.RemoveAt(i); clipped = true; break; } // a point on a straight run: drop it
                    if (ccw ? cr < 0f : cr > 0f) continue;                                 // a reflex corner is no ear
                    bool inside = false;
                    for (int j = 0; j < idx.Count && !inside; j++)
                    {
                        int q = idx[j];
                        if (q == ia || q == ib || q == ic) continue;
                        inside = InTri(poly[q], a, b, c);
                    }
                    if (inside) continue;
                    res.Add(a); res.Add(b); res.Add(c);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { res.Add(poly[idx[0]]); res.Add(poly[idx[1]]); res.Add(poly[idx[2]]); }
            return res;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
    }
}
