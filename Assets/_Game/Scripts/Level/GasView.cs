using Stealth.Configs;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// How the gas outside the house looks (visual only: the gas damage, GasExposure, still hurts wherever it did).
    /// The gas is a field in the world, drawn by the gas pass (Art/Shaders/GasFog.shader, before post-processing): none at
    /// the outer walls, thickening linearly to full <see cref="StyleWorld.GasClear"/> metres out, a layer over the ground
    /// that thins out with height — so the sky panorama (<see cref="StyleWorld.Sky"/>) shows above it, through doors and
    /// windows (real holes with glass, LevelBuilder.Wall). The same density in C# is here for the tests.
    /// </summary>
    public static class GasView
    {
        static readonly int On = Shader.PropertyToID("_GasOn");
        static readonly int Box = Shader.PropertyToID("_GasBox");
        static readonly int Params = Shader.PropertyToID("_GasParams");
        static readonly int Colour = Shader.PropertyToID("_GasColor");
        static readonly int SkyTex = Shader.PropertyToID("_GasSky");
        static readonly int SkyParams = Shader.PropertyToID("_GasSkyParams");

        /// <summary>The house's footprint the gas keeps away from: centre and half size on the ground (x, z).</summary>
        public static Vector4 BoxOf(LevelLayout l)
        {
            var b = l.BuildingBounds;
            return new Vector4(b.center.x, b.center.z, b.extents.x, b.extents.z);
        }

        /// <summary>Gas density at a point (per metre; pure): the same formula as GasCommon.hlsl.</summary>
        public static float Density(Vector3 p, Vector4 box, StyleWorld w, float groundY = 0f)
        {
            float qx = Mathf.Max(Mathf.Abs(p.x - box.x) - box.z, 0f);
            float qz = Mathf.Max(Mathf.Abs(p.z - box.y) - box.w, 0f);
            float ramp = Mathf.Clamp01(Mathf.Sqrt(qx * qx + qz * qz) / Mathf.Max(w.GasClear, 0.01f));
            if (p.y < groundY + LevelLayout.Underground) return 0f; // underground: the basement's tunnels, the sewer
            float above = Mathf.Max(p.y - groundY - w.GasLayer, 0f);
            return w.GasDensity * ramp * Mathf.Exp(-above / Mathf.Max(w.GasFalloff, 0.01f));
        }

        /// <summary>The gas and the sky around this house, for this frame; <paramref name="cameraInside"/>: windows show the outside.</summary>
        public static void Show(LevelLayout l, StyleWorld w, bool cameraInside)
        {
            Shader.SetGlobalFloat(On, 1f);
            Shader.SetGlobalVector(Box, BoxOf(l));
            Shader.SetGlobalVector(Params, new Vector4(w.GasDensity, w.GasClear, w.GasLayer, w.GasFalloff));
            var c = w.GasFog;
            Shader.SetGlobalVector(Colour, new Vector4(c.r, c.g, c.b, l.BuildingBounds.min.y));
            if (w.Sky != null) Shader.SetGlobalTexture(SkyTex, w.Sky);
            Shader.SetGlobalVector(SkyParams, new Vector4(w.Sky != null ? w.SkyExposure : 0f, cameraInside ? 1f : 0f, w.SkyDistance, 0f));
        }

        /// <summary>No house (the menu, the sewer): the pass only copies the image.</summary>
        public static void Hide() => Shader.SetGlobalFloat(On, 0f);
    }
}
