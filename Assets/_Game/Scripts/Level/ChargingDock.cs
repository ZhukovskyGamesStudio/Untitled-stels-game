using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A charging dock of the robots' charging room (prefab Props/ChargingDock): its glowing parts breathe slowly while it is
    /// free, and with a robot resting on it they pulse like charge flowing in — a wave running up again and again. Visual
    /// only, every peer (a resting robot is replicated: <see cref="RobotController.RestingShown"/>). Its glowing parts carry
    /// a property block, which keeps them out of the merged furniture.
    /// </summary>
    public class ChargingDock : MonoBehaviour
    {
        [Tooltip("The glowing parts (Eye*), found on start when empty.")]
        public Renderer[] Glow;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        static readonly int MovingId = Shader.PropertyToID("_StorageMoving");
        MaterialPropertyBlock m_Block;
        Color[] m_Base;
        float m_NextLook, m_Charge;
        bool m_Busy;

        // (in Awake: the block must be there before the house merges its furniture, or the glow is baked in and never moves)
        void Awake()
        {
            if (Glow == null || Glow.Length == 0)
            {
                var list = new System.Collections.Generic.List<Renderer>();
                foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Eye")) list.Add(r);
                Glow = list.ToArray();
            }
            m_Block = new MaterialPropertyBlock();
            m_Base = new Color[Glow.Length];
            for (int i = 0; i < Glow.Length; i++)
                m_Base[i] = Glow[i] != null && Glow[i].sharedMaterial != null && Glow[i].sharedMaterial.HasProperty(EmissionId)
                    ? Glow[i].sharedMaterial.GetColor(EmissionId) : Color.white;
            for (int i = 0; i < Glow.Length; i++)
            {
                if (Glow[i] == null) continue;
                Glow[i].GetPropertyBlock(m_Block);
                m_Block.SetFloat(MovingId, 1f);
                Glow[i].SetPropertyBlock(m_Block);
            }
        }

        /// <summary>The glow's strength (pure): a slow breath while free, a rising pulse while a robot charges.</summary>
        public static float Strength(float time, bool charging) =>
            charging ? 0.55f + 0.9f * Mathf.Pow(Mathf.Repeat(time * 0.9f, 1f), 2f) : 0.25f + 0.1f * Mathf.Sin(time * 1.3f);

        void Update()
        {
            if (Glow == null || m_Block == null) return;
            if (Time.time >= m_NextLook)
            {
                m_NextLook = Time.time + 0.5f;
                m_Busy = false;
                foreach (var r in RobotController.Robots)
                    if (r != null && r.RestingShown && (r.transform.position - transform.position).sqrMagnitude < 1.6f * 1.6f) { m_Busy = true; break; }
            }
            m_Charge = Mathf.MoveTowards(m_Charge, m_Busy ? 1f : 0f, Time.deltaTime * 2f);
            float k = Mathf.Lerp(Strength(Time.time, false), Strength(Time.time, true), m_Charge);
            for (int i = 0; i < Glow.Length; i++)
            {
                if (Glow[i] == null) continue;
                Glow[i].GetPropertyBlock(m_Block);
                m_Block.SetFloat(MovingId, 1f);
                m_Block.SetColor(EmissionId, m_Base[i] * k);
                Glow[i].SetPropertyBlock(m_Block);
            }
        }
    }
}
