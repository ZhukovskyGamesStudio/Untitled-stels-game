// Inputs of Stealth/Surface: the names URP's own passes use (this file stands in for LitInput.hlsl: same guard), and the
// wear laid over a surface (fingerprints, scratches, stains, streaks; worn edges; dirt in corners and at the floor).
#ifndef UNIVERSAL_LIT_INPUT_INCLUDED
#define UNIVERSAL_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _WearColor;
half4 _DirtColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
float _WearTile;
half _Smudges;
half _Scratches;
half _Stains;
half _Streaks;
half _EdgeWear;
half _WearMetal;
half _WearSmooth;
half _Corners;
half _FloorDirt;
float _FloorHeight;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_WearMap);            SAMPLER(sampler_WearMap);

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 m = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    m.a *= _Smoothness;
    return m;
}

// The bare surface, without wear (URP's own lit pass, included for its vertices, wants it).
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData s)
{
    s = (SurfaceData)0;
    half4 mask = SampleMetallicSpecGloss(uv, 1.0);
    s.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
    s.alpha = 1.0;
    s.metallic = mask.r;
    s.smoothness = mask.a;
    s.occlusion = LerpWhiteTo(mask.g, _OcclusionStrength);
    s.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
}

#endif
