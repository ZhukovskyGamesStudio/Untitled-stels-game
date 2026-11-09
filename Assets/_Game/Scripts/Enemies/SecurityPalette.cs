using System;
using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>Status colours shared by every robot and device: one visual language for "calm / suspicious / alarm".</summary>
    public static class SecurityPalette
    {
        public static readonly Color Calm = new(0.2f, 0.85f, 1f);
        public static readonly Color Suspicious = new(1f, 0.85f, 0.1f);
        public static readonly Color Investigating = new(1f, 0.5f, 0.1f);
        public static readonly Color Alarm = new(1f, 0.1f, 0.05f);
        public static readonly Color Hacked = new(0.12f, 0.12f, 0.12f);

        /// <summary>Device alert level (0 idle, 1 noticing, 2 detected) → colour.</summary>
        public static Color ForAlert(byte alert, Color idle) => alert switch
        {
            0 => idle,
            1 => Suspicious,
            _ => Alarm,
        };

        public static Color ForState(RobotState state, Color calm) => state switch
        {
            RobotState.Patrol => calm,
            RobotState.Suspicious => Suspicious,
            RobotState.Investigate => Investigating,
            _ => Alarm,
        };
    }

    /// <summary>
    /// Glowing parts (eyes, lenses) and an optional point light that show a robot's status.
    /// Uses a property block, so the shared materials stay untouched.
    /// </summary>
    [Serializable]
    public class StatusLights
    {
        public Renderer[] Renderers = Array.Empty<Renderer>();
        public Light Light;
        [Tooltip("Emission multiplier of the status colour.")]
        public float Emission = 2.5f;

        MaterialPropertyBlock m_Block;

        public Color Current { get; private set; }

        public void Apply(Color c)
        {
            Current = c;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", c);
            m_Block.SetColor("_EmissionColor", c * Emission);
            if (Renderers != null)
                foreach (var r in Renderers)
                    if (r != null) r.SetPropertyBlock(m_Block);
            if (Light != null) Light.color = c;
        }
    }
}
