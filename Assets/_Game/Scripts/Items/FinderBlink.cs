using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// The fuel finder's lamp (on the prefab Items/FinderModel): in a player's hand it flashes, the faster the closer the
    /// nearest loot lying in the house (<see cref="GearRules.FinderPeriod"/>); silent, but the flash is a real light with a
    /// <see cref="Level.LightSource"/>, so it lights its holder up for the robots. Lying on the floor it is dark.
    /// Every peer runs it from the same replicated items, so the server's robots see the flash where everyone does.
    /// </summary>
    public class FinderBlink : MonoBehaviour
    {
        [Tooltip("The flash (point light with a LightSource).")]
        public Light Lamp;
        [Tooltip("The lamp's glass: lit with the flash.")]
        public Renderer Glass;

        float m_Phase, m_Period = 1f, m_NextLook;
        PlayerController m_Holder;
        bool m_Lit;
        MaterialPropertyBlock m_Block;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void OnEnable()
        {
            m_Holder = GetComponentInParent<PlayerController>();
            SetLit(false);
        }

        void Update()
        {
            if (m_Holder == null || m_Holder.Downed) { SetLit(false); return; }
            if (Time.time >= m_NextLook)
            {
                m_NextLook = Time.time + 0.25f;
                m_Period = GearRules.FinderPeriod(NearestLoot(transform.position), Tuning.Items.Gadgets);
            }
            m_Phase += Time.deltaTime;
            if (m_Phase >= m_Period) m_Phase -= m_Period * Mathf.Floor(m_Phase / m_Period);
            SetLit(GearRules.FinderLit(m_Phase, m_Period));
        }

        /// <summary>Metres to the nearest loot lying in the world (∞: none).</summary>
        static float NearestLoot(Vector3 from)
        {
            float best = float.PositiveInfinity;
            foreach (var item in WorldItem.All)
            {
                if (item == null || !ItemCatalog.IsLoot(item.Shape)) continue;
                float d = Vector3.Distance(from, item.transform.position);
                if (d < best) best = d;
            }
            return best;
        }

        void SetLit(bool lit)
        {
            if (lit == m_Lit && Lamp != null && Lamp.enabled == lit) return;
            m_Lit = lit;
            if (Lamp != null) Lamp.enabled = lit;
            if (Glass == null) return;
            m_Block ??= new MaterialPropertyBlock();
            Glass.GetPropertyBlock(m_Block);
            m_Block.SetColor(EmissionId, lit ? new Color(0.4f, 3f, 0.6f) : new Color(0.02f, 0.08f, 0.03f));
            Glass.SetPropertyBlock(m_Block);
        }
    }
}
