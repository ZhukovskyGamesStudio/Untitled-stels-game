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
        /// <summary>A robot that hears: its ear lamps glow this while it is calm (the eyes glow <see cref="Calm"/>).</summary>
        public static readonly Color Hearing = new(1f, 0.74f, 0.18f);
        /// <summary>A lamp that does not glow: dark glass.</summary>
        public static readonly Color Off = new(0.05f, 0.055f, 0.06f);

        /// <summary>
        /// What glows on a robot (pure, unit tested): the eye (the visual sensor) in its colour, the ear lamps yellow while
        /// calm, both in the alert colour when it is not; what a robot does not have stays dark, and so does its light.
        /// </summary>
        public static (Color eye, Color ear, Color light, bool lit) Glow(Color status, bool calm, bool eyes, bool ears) =>
        (
            eyes ? status : Off,
            ears ? (calm ? Hearing : status) : Off,
            eyes ? status : ears ? (calm ? Hearing : status) : Off,
            eyes || ears
        );

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
    /// The glowing parts that show what a robot senses and its status (<see cref="SecurityPalette.Glow"/>): the eye — the
    /// visual sensor — blue, the ear lamps yellow, the rest never lit; and an optional light in the leading colour (off on a
    /// robot with neither). Uses a property block, so the shared materials stay untouched.
    /// </summary>
    [Serializable]
    public class StatusLights
    {
        [Tooltip("The visual sensor's lenses: lit when the robot sees.")]
        public Renderer[] Renderers = Array.Empty<Renderer>();
        [Tooltip("The ear lamps (the body's own glowing parts on a mobile robot): yellow when the robot hears.")]
        public Renderer[] Ears = Array.Empty<Renderer>();
        [Tooltip("Glowing parts that stay dark (the nose, the ground probes, a lift's lamps).")]
        public Renderer[] Dark = Array.Empty<Renderer>();
        public Light Light;
        [Tooltip("Emission multiplier of the status colour.")]
        public float Emission = 2.5f;

        MaterialPropertyBlock m_Block;

        /// <summary>The leading colour now (the eye's, else the ears'; dark without either).</summary>
        public Color Current { get; private set; }

        public void Apply(Color status, bool calm, bool eyes, bool ears)
        {
            var (eye, ear, light, lit) = SecurityPalette.Glow(status, calm, eyes, ears);
            Current = light;
            m_Block ??= new MaterialPropertyBlock();
            Paint(Renderers, eye, eyes);
            Paint(Ears, ear, ears);
            Paint(Dark, SecurityPalette.Off, false);
            if (Light != null)
            {
                Light.color = light;
                if (Light.enabled != lit) Light.enabled = lit;
            }
        }

        /// <summary>The state of a device with no eye and no ear (<see cref="Detector.StateLine"/>): its parts in the status colour, dimly lit of themselves; no light.</summary>
        public void Line(Color status, float share)
        {
            if (Renderers == null) return;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", Color.Lerp(SecurityPalette.Off, status, 0.5f));
            m_Block.SetColor("_EmissionColor", status * Mathf.Clamp(share, 0f, 2f));
            foreach (var r in Renderers)
                if (r != null) r.SetPropertyBlock(m_Block);
            Current = share > 0f ? status * Mathf.Clamp(share, 0f, 2f) : SecurityPalette.Off;
        }

        void Paint(Renderer[] parts, Color c, bool glows)
        {
            if (parts == null) return;
            m_Block.SetColor("_BaseColor", c);
            m_Block.SetColor("_EmissionColor", glows ? c * Emission : Color.black);
            foreach (var r in parts)
                if (r != null) r.SetPropertyBlock(m_Block);
        }
    }
}
