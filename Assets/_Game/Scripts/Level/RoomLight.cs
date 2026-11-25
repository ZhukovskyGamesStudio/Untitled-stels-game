using Stealth.Game;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Ceiling lights of a room: two glowing panels and one point light between them with soft shadows (walls and slabs
    /// keep the light inside the room). Where the style asks (<see cref="LightSource.CeilingGlow"/>) the lamp looks soft:
    /// a hot middle on each diffuser fading to its edges (<see cref="Cores"/>) and a pool of light on the ceiling round
    /// the fixture (<see cref="Halo"/>) — glow drawn over the picture, no light cast. In a dark room it is off
    /// (the panel too); a broken one flickers; during an alarm every lamp pulses red, even in dark rooms. Purely visual,
    /// runs on every peer; robots read the result through its <see cref="LightSource"/>.
    /// </summary>
    public class RoomLight : MonoBehaviour
    {
        [Tooltip("The light of the room (a child under the ceiling).")]
        public Light Lamp;
        public bool Flicker;
        public float Seed;
        [Tooltip("The glowing diffusers of the panels: their emission follows the light.")]
        public Renderer[] Panels;
        [Tooltip("The hot middle of each diffuser (additive, soft): shown where the style has soft ceiling lamps.")]
        public Renderer[] Cores;
        [Tooltip("The pool of light on the ceiling round the fixture (additive, soft).")]
        public Renderer Halo;
        [Tooltip("The lamp's cookies: none of its light goes up (a recessed panel), and — where the style has soft lamps — a share of it does: the lamp lights its own ceiling and the tops of its walls, with shadows, so nothing of it reaches the next room.")]
        public Texture Cookie, SoftCookie;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color AlarmColor = new(1f, 0.15f, 0.1f);


        Light m_Light;
        Color m_BaseColor, m_LitColor;
        float m_BaseIntensity, m_LitIntensity;
        int m_Room = -1;
        /// <summary>The room it lights (its id in the plan; −1: none).</summary>
        public int Room => m_Room;
        bool m_Dark;
        MaterialPropertyBlock m_Block, m_GlowBlock;

        /// <summary>Every ceiling lamp that stands in the world (the room fill sums them: RoomFillField).</summary>
        public static readonly System.Collections.Generic.List<RoomLight> All = new();
        /// <summary>What the lamp gives now, steady (linear colour × intensity; no flicker, no alarm pulse): black when off.</summary>
        public Color Output { get; private set; }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        float m_LastGlow = -1f, m_LastSoft = -1f;
        Color m_LastColor;

        public void Init(Color color, float intensity, bool flicker, float seed) => Init(color, intensity, flicker, seed, -1, false, color, intensity);

        /// <summary>
        /// A lamp of room <paramref name="room"/>: <paramref name="color"/>/<paramref name="intensity"/> as the house starts
        /// (<paramref name="dark"/>: off or dim), the lit look when a switch turns a dark room on, nothing when it turns a lit one off.
        /// </summary>
        public void Init(Color color, float intensity, bool flicker, float seed, int room, bool dark, Color litColor, float litIntensity)
        {
            m_Light = Lamp != null ? Lamp : GetComponentInChildren<Light>();
            m_BaseColor = color;
            m_BaseIntensity = intensity;
            m_Room = room;
            m_Dark = dark;
            m_LitColor = litColor;
            m_LitIntensity = litIntensity;
            Flicker = flicker;
            Seed = seed;
            Output = color.linear * intensity;
            Apply(color, intensity);
        }

        void Update()
        {
            if (m_Light == null) return;
            float k = Flicker ? LightMath.Flicker(Time.time + Seed) : 1f;
            var gm = GameManager.Instance;
            bool alarm = gm != null && gm.AlarmActive;
            bool switched = m_Room >= 0 && World.IsOpened(OpenKeys.Lights(m_Room));
            var own = switched ? m_LitColor : m_BaseColor;
            float ownIntensity = !switched ? m_BaseIntensity : LightMath.SwitchedOn(m_Dark, true) ? m_LitIntensity : 0f;
            var color = alarm ? Color.Lerp(own, AlarmColor, 0.8f) : own;
            float baseIntensity = alarm ? Mathf.Max(ownIntensity, LightMath.AlarmIntensity) : ownIntensity; // alarm lights even dark rooms
            Output = color.linear * (baseIntensity * (alarm ? 0.7f : 1f));
            Apply(color, baseIntensity * k * (alarm ? 0.7f + 0.3f * Mathf.Sin(Time.time * 6f) : 1f));
        }

        void Apply(Color color, float intensity)
        {
            m_Light.color = color;
            m_Light.intensity = intensity;
            m_Light.enabled = intensity > 0.001f;
            if (Panels == null || Panels.Length == 0) return;
            // The panel glows with the lamp (property block: the shared material stays untouched).
            float lit = Mathf.Clamp01(intensity / LightMath.RoomIntensity);
            float glow = lit * LightSource.GlowScale; // the style's look of the diffusers
            float soft = lit * LightSource.CeilingGlow;
            var cookie = LightSource.CeilingGlow > 0f && SoftCookie != null ? SoftCookie : Cookie;
            if (cookie != null && m_Light.cookie != cookie) m_Light.cookie = cookie;
            if (Mathf.Abs(glow - m_LastGlow) < 0.02f && Mathf.Abs(soft - m_LastSoft) < 0.02f && color == m_LastColor) return;
            m_LastGlow = glow;
            m_LastSoft = soft;
            m_LastColor = color;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor(EmissionId, color * (LightMath.PanelGlow * glow));
            foreach (var p in Panels) if (p != null) p.SetPropertyBlock(m_Block);
            // the soft look: the diffusers' hot middle and the light on the ceiling round the fixture (linear values, as
            // vectors: a property block's colours are taken as sRGB)
            bool show = soft > 0.01f;
            var linear = color.linear;
            m_GlowBlock ??= new MaterialPropertyBlock();
            if (Cores != null)
                foreach (var c in Cores)
                {
                    if (c == null) continue;
                    if (c.enabled != show) c.enabled = show;
                    if (!show) continue;
                    m_GlowBlock.SetVector(BaseColorId, LightMath.Glow(linear, LightMath.CoreGlow * soft));
                    c.SetPropertyBlock(m_GlowBlock);
                }
            if (Halo == null) return;
            if (Halo.enabled != show) Halo.enabled = show;
            if (!show) return;
            m_GlowBlock.SetVector(BaseColorId, LightMath.Glow(linear, LightMath.HaloGlow * soft));
            Halo.SetPropertyBlock(m_GlowBlock);
        }
    }

    /// <summary>Light rules (pure, unit tested): flicker, URP-like falloff, how visible a lit / dark player is.</summary>
    public static class LightMath
    {
        /// <summary>Night: no light comes from nowhere. A closed room without lamps is pitch black.</summary>
        public static readonly Color NightAmbient = Color.black;
        /// <summary>Fog indoors (dark, only takes the distance away) and the gas outside.</summary>
        public static readonly Color NightFog = new(0.012f, 0.014f, 0.02f);
        public static readonly Color GasFog = new(0.33f, 0.37f, 0.3f);
        /// <summary>Exponential-squared density of the gas outside: ~95% fog at 1.5 m.</summary>
        public const float GasFogDensity = 1.15f;

        /// <summary>Intensity of a normal ceiling lamp (the panel glows fully at it).</summary>
        public const float RoomIntensity = 3.6f;
        /// <summary>Every lamp gets at least this during an alarm.</summary>
        public const float AlarmIntensity = 1.4f;
        /// <summary>Emission of a lit panel (HDR, catches the bloom).</summary>
        public const float PanelGlow = 2.2f;
        /// <summary>The hot middle of a soft lamp's diffuser, added over its even glow (linear: over 1 with the panel's own, it blooms a little).</summary>
        public const float CoreGlow = 1.1f;
        /// <summary>The glow added on the ceiling right round a soft lamp (linear): a little — the lamp's own light is on the ceiling there (<see cref="SoftCeilingShare"/>).</summary>
        public const float HaloGlow = 0.1f;
        /// <summary>
        /// How much of a soft lamp's light goes up, share of what goes down: its room's ceiling and the tops of its walls
        /// are lit by it — the room's own fill, cast with the lamp's shadows (a dark room next door gets none of it).
        /// </summary>
        public const float SoftCeilingShare = 0.35f;

        /// <summary>A lamp's cookie in a direction (pure): full down and sideways, <paramref name="up"/> of it above the horizon.</summary>
        public static float Cookie(float dirY, float up) => Mathf.Lerp(up, 1f, CookieUpFade(dirY));

        /// <summary>An additive glow's colour (linear, alpha 1): the lamp's colour at this strength.</summary>
        public static Vector4 Glow(Color linear, float strength) => new(linear.r * strength, linear.g * strength, linear.b * strength, 1f);

        /// <summary>Whether a room's lamps are on (pure): a room that started lit goes dark when switched, a dark one lights up.</summary>
        public static bool SwitchedOn(bool startedDark, bool switched) => startedDark == switched;

        /// <summary>Ceiling lamp cookie (pure): full light below the horizon, none straight up (dir.y = direction's height).</summary>
        public static float CookieUpFade(float dirY) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, 0.2f, dirY));

        /// <summary>
        /// The intensity the game set (pure): if the light still holds what the style scale last wrote, the raw value is the
        /// remembered one; if anything else wrote to it since (lamp logic, flicker, alarm, a prefab), that new value is raw.
        /// </summary>
        public static float RawIntensity(float current, float lastRaw, float lastShown) =>
            lastRaw >= 0f && Mathf.Abs(current - lastShown) < 1e-4f ? lastRaw : current;

        /// <summary>Broken fluorescent tube: mostly on, with short irregular drop-outs. Returns 0..1.</summary>
        public static float Flicker(float t)
        {
            float n = Mathf.PerlinNoise(t * 3.1f, 0.37f);
            float burst = Mathf.PerlinNoise(t * 17f, 5.1f);
            return n < 0.3f ? (burst > 0.5f ? 0.05f : 0.6f) : 1f;
        }

        /// <summary>
        /// How a point / spot light fades with distance, as URP draws it: inverse square, smoothly cut to 0 at the range.
        /// </summary>
        public static float Attenuation(float distance, float range)
        {
            if (range <= 0f || distance >= range) return 0f;
            float d2 = distance * distance;
            float f = d2 / (range * range);
            float smooth = Mathf.Clamp01(1f - f * f);
            return smooth * smooth / Mathf.Max(d2, 0.25f); // closer than 0.5 m counts as 0.5 m: no infinite hot spots
        }

        /// <summary>Spot cone: 1 inside the inner angle, fading to 0 at the outer one (degrees, full angles).</summary>
        public static float SpotFactor(float angleFromAxis, float innerAngle, float outerAngle)
        {
            float cosA = Mathf.Cos(angleFromAxis * Mathf.Deg2Rad);
            float cosOuter = Mathf.Cos(outerAngle * 0.5f * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(Mathf.Min(innerAngle, outerAngle - 0.1f) * 0.5f * Mathf.Deg2Rad);
            float t = Mathf.Clamp01((cosA - cosOuter) / Mathf.Max(cosInner - cosOuter, 1e-4f));
            return t * t;
        }
    }
}
