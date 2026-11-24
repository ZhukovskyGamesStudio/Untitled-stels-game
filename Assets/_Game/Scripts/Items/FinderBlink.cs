using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// The fuel finder's lamp (on the prefabs Items/FinderModel and Items/ProfinderModel): in a player's hand it flashes,
    /// the faster the closer the nearest loot lying in the house. The plain finder reads loot ahead of where the holder
    /// looks as nearer (<see cref="GearRules.FinderReading"/>); the upgraded one (<see cref="Segments"/>) reads the true
    /// distance and lights the lamps of its ring that point the way the loot lies, the wider and vaguer the further it is
    /// (<see cref="CraftedRules.DialGlow"/>). Silent, but the flash is a real light with a <see cref="Level.LightSource"/>,
    /// so it lights its holder up for the robots. Lying on the floor it is dark. Every peer runs it from the same
    /// replicated items, so the server's robots see the flash where everyone does.
    /// </summary>
    public class FinderBlink : MonoBehaviour
    {
        [Tooltip("The flash (point light with a LightSource).")]
        public Light Lamp;
        [Tooltip("The lamp's glass: lit with the flash (the upgraded finder: the lamp in the middle of the ring).")]
        public Renderer Glass;
        [Tooltip("The upgraded finder's ring of lamps round the Glass (empty: the plain finder).")]
        public Renderer[] Segments;

        float m_Phase, m_Period = 1f, m_NextLook;
        PlayerController m_Holder;
        float m_Glass = -1f;
        MaterialPropertyBlock m_Block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        // the ring: each lamp's centre and the dial's, in this model's space; where the loot is and how far; what is shown
        Vector3[] m_SegmentAt;
        Vector3 m_DialAt, m_LootAt;
        float m_LootDistance = float.PositiveInfinity;
        float[] m_Shown;

        bool Ring => Segments != null && Segments.Length > 0;

        void OnEnable()
        {
            m_Holder = GetComponentInParent<PlayerController>();
            m_Glass = 1f;
            SetLit(false, 0f);
            DimForHolder(true);
            if (!Ring) return;
            m_SegmentAt = new Vector3[Segments.Length];
            m_Shown = new float[Segments.Length];
            for (int i = 0; i < Segments.Length; i++)
            {
                m_SegmentAt[i] = Segments[i] != null ? transform.InverseTransformPoint(Segments[i].bounds.center) : Vector3.zero;
                m_Shown[i] = -1f;
            }
            m_DialAt = Glass != null ? transform.InverseTransformPoint(Glass.bounds.center) : Vector3.zero;
            m_LootDistance = float.PositiveInfinity;
            ShowRing(0f);
        }

        void OnDisable() => DimForHolder(false);

        /// <summary>
        /// The flash in its holder's own view shows only <see cref="GadgetSettings.FinderLampShown"/> of its light
        /// (<see cref="Level.LightSource.VisualShare"/>: a lamp a hand's breadth from the camera burnt the picture out): the
        /// teammates see it as ever, and the robots read its true brightness on the server.
        /// </summary>
        void DimForHolder(bool on)
        {
            if (Lamp == null || !Lamp.TryGetComponent<Level.LightSource>(out var source)) return;
            source.VisualShare = on && m_Holder != null && m_Holder.IsOwner ? Tuning.Items.Gadgets.FinderLampShown : 1f;
        }

        void Update()
        {
            if (m_Holder == null || m_Holder.Downed)
            {
                SetLit(false, 0f);
                if (Ring) ShowRing(0f);
                return;
            }
            var s = Tuning.Items.Gadgets;
            if (Time.time >= m_NextLook)
            {
                m_NextLook = Time.time + 0.25f;
                if (Ring)
                {
                    var dial = transform.TransformPoint(m_DialAt);
                    m_LootDistance = NearestLoot(dial, out m_LootAt);
                    m_Period = GearRules.FinderPeriod(m_LootDistance, s);
                }
                else
                {
                    var look = m_Holder.Head != null ? m_Holder.Head.forward : m_Holder.transform.forward;
                    m_Period = GearRules.FinderPeriod(NearestLoot(transform.position, look, s), s);
                }
            }
            m_Phase += Time.deltaTime;
            if (m_Phase >= m_Period) m_Phase -= m_Period * Mathf.Floor(m_Phase / m_Period);
            // (the glass comes up at once and dies away: no hard strobe before the eyes; the ring follows it)
            float glass = GearRules.FinderGlass(m_Phase, m_Period);
            SetLit(GearRules.FinderLit(m_Phase, m_Period), glass);
            if (Ring) ShowRing(Mathf.Max(glass, Tuning.Items.Crafted.FinderSteady));
        }

        /// <summary>
        /// The best reading among the loot lying in the world (∞: none): the distance, longer for loot away from where the
        /// holder looks (<see cref="GearRules.FinderReading"/>; on the level: the same on every peer, the yaw is replicated).
        /// </summary>
        static float NearestLoot(Vector3 from, Vector3 look, GadgetSettings s)
        {
            look.y = 0f;
            look = look.sqrMagnitude > 1e-6f ? look.normalized : Vector3.forward;
            float best = float.PositiveInfinity;
            foreach (var item in WorldItem.All)
            {
                if (item == null || !ItemCatalog.IsLoot(item.Shape)) continue;
                var to = item.transform.position - from;
                float d = to.magnitude;
                to.y = 0f;
                float cos = to.sqrMagnitude > 1e-4f ? Vector3.Dot(to.normalized, look) : 1f;
                float reading = GearRules.FinderReading(d, cos, s);
                if (reading < best) best = reading;
            }
            return best;
        }

        /// <summary>The nearest loot lying in the world, by its true distance (∞: none), and where it is.</summary>
        static float NearestLoot(Vector3 from, out Vector3 at)
        {
            float best = float.PositiveInfinity;
            at = from;
            foreach (var item in WorldItem.All)
            {
                if (item == null || !ItemCatalog.IsLoot(item.Shape)) continue;
                var p = item.transform.position;
                float d = (p - from).magnitude;
                if (d < best) { best = d; at = p; }
            }
            return best;
        }

        /// <summary>The ring's lamps, each as bright as it points the way to the loot, × <paramref name="pulse"/>.</summary>
        void ShowRing(float pulse)
        {
            var crafted = Tuning.Items.Crafted;
            var dial = transform.TransformPoint(m_DialAt);
            var lootWay = m_LootAt - dial;
            for (int i = 0; i < Segments.Length; i++)
            {
                var r = Segments[i];
                if (r == null) continue;
                float glow = pulse <= 0f ? 0f : CraftedRules.DialGlow(transform.TransformVector(m_SegmentAt[i] - m_DialAt), lootWay, m_LootDistance, crafted) * pulse;
                glow = Mathf.Round(glow * 32f) / 32f;
                if (Mathf.Approximately(glow, m_Shown[i])) continue;
                m_Shown[i] = glow;
                Paint(r, glow);
            }
        }

        /// <summary>A lamp's glow, 0 (dark) .. 1 (three quarters of its material's own glow).</summary>
        void Paint(Renderer r, float glow)
        {
            m_Block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(m_Block);
            var c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(EmissionId) ? r.sharedMaterial.GetColor(EmissionId) : Color.green;
            m_Block.SetColor(EmissionId, Color.Lerp(c * 0.02f, c * 0.75f, glow)); // (a lamp, not a photo flash: barely blooms)
            r.SetPropertyBlock(m_Block);
        }

        void SetLit(bool lit, float glass)
        {
            if (Lamp != null && Lamp.enabled != lit) Lamp.enabled = lit;
            if (Glass == null || Mathf.Approximately(glass, m_Glass)) return;
            m_Glass = glass;
            if (Ring) { Paint(Glass, glass); return; } // (the ring's middle lamp: its material's own glow)
            m_Block ??= new MaterialPropertyBlock();
            Glass.GetPropertyBlock(m_Block);
            m_Block.SetColor(EmissionId, Color.Lerp(new Color(0.02f, 0.08f, 0.03f), new Color(0.15f, 1f, 0.22f) * Tuning.Items.Gadgets.FinderGlow, glass));
            Glass.SetPropertyBlock(m_Block);
        }
    }
}
