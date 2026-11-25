using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Contact shadows of the picture (Art/Shaders/ScreenBounce.shader): a small thing on a desk is too thin for a lamp's
    /// shadow map and seems to float, so the full-screen pass walks a hand's breadth from every pixel towards the nearest
    /// ceiling lamps over the depth of the screen, and darkens the pixel by the share of the light something stands in
    /// the way of. Hands the pass those lamps. A picture only: robots read the light through <see cref="LightSource"/>.
    /// </summary>
    public static class ContactShadows
    {
        /// <summary>How many lamps the pass walks towards.</summary>
        public const int Lamps = 8;
        /// <summary>Lamps farther than this from the camera are left out, metres.</summary>
        public const float Reach = 12f;

        static readonly int LampsId = Shader.PropertyToID("_ContactLamps"), CountId = Shader.PropertyToID("_ContactCount"), StrengthId = Shader.PropertyToID("_ContactStrength");
        static readonly Vector4[] s_Lamps = new Vector4[Lamps];
        static readonly List<(float distance, Vector4 lamp)> s_Near = new();

        /// <summary>How dark the style draws them (StyleGrade.Contact; 0 = none).</summary>
        public static float Strength
        {
            get => s_Strength;
            set
            {
                s_Strength = Mathf.Clamp01(value);
                Shader.SetGlobalFloat(StrengthId, s_Strength);
            }
        }
        static float s_Strength;

        /// <summary>The lamps to walk towards (pure): the burning ones nearest the camera, at most <see cref="Lamps"/>, within <see cref="Reach"/>.</summary>
        public static int Choose(IReadOnlyList<(Vector3 at, float light)> lamps, Vector3 camera, Vector4[] into)
        {
            s_Near.Clear();
            for (int i = 0; i < lamps.Count; i++)
            {
                float d = Vector3.Distance(lamps[i].at, camera);
                if (lamps[i].light > 0.01f && d <= Reach) s_Near.Add((d, new Vector4(lamps[i].at.x, lamps[i].at.y, lamps[i].at.z, lamps[i].light)));
            }
            s_Near.Sort((a, b) => a.distance.CompareTo(b.distance));
            int n = Mathf.Min(s_Near.Count, into.Length);
            for (int i = 0; i < n; i++) into[i] = s_Near[i].lamp;
            return n;
        }

        static readonly List<(Vector3, float)> s_All = new();

        /// <summary>The house's ceiling lamps as they burn now (ticked by <see cref="LightBudget"/>).</summary>
        public static void Tick(Vector3 camera)
        {
            s_All.Clear();
            if (s_Strength > 0f)
                foreach (var lamp in RoomLight.All)
                {
                    if (lamp == null) continue;
                    var c = lamp.Output;
                    s_All.Add((lamp.Lamp != null ? lamp.Lamp.transform.position : lamp.transform.position, 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b));
                }
            Show(s_All, camera);
        }

        /// <summary>Hands the pass these lamps (the picture tools: a room without a house).</summary>
        public static void Show(IReadOnlyList<(Vector3 at, float light)> lamps, Vector3 camera)
        {
            int n = Choose(lamps, camera, s_Lamps);
            Shader.SetGlobalVectorArray(LampsId, s_Lamps);
            Shader.SetGlobalInt(CountId, n);
        }
    }
}
