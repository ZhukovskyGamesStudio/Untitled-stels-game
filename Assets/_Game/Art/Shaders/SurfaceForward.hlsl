// The lit pass of Stealth/Surface: URP's lighting of a surface with wear worked into it.
#ifndef STEALTH_SURFACE_FORWARD_INCLUDED
#define STEALTH_SURFACE_FORWARD_INCLUDED

#include "SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

// How much of an outer edge a pixel is (0 a face or a hollow .. 1 a right angle): the normals and places a few millimetres
// to either side, from the normals the ambient occlusion has drawn already — nothing is baked, any shape will do, whether
// its bevels are smooth, faceted or missing. Fades out where the edge is thinner than a pixel.
half OuterEdge(float2 uv, float3 positionWS)
{
    const float reach = 0.0055;
    float away = max(dot(positionWS - GetCameraPositionWS(), -UNITY_MATRIX_V[2].xyz), 0.05);
    float tall = reach * abs(UNITY_MATRIX_P._m11) / (2.0 * away);       // the reach, share of the picture's height
    float px = tall * _ScaledScreenParams.y;
    half fade = saturate(px * 1.1 - 0.35);
    if (fade <= 0.0) return 0.0;
    float2 o = max(px, 1.0) * (_ScaledScreenParams.zw - 1.0);
    half best = 0.0;
    [unroll] for (int k = 0; k < 2; k++)
    {
        float2 d = k == 0 ? float2(o.x, 0.0) : float2(0.0, o.y);
        float2 ua = uv + d, ub = uv - d;
        float3 pa = ComputeWorldSpacePosition(ua, SampleSceneDepth(ua), UNITY_MATRIX_I_VP);
        float3 pb = ComputeWorldSpacePosition(ub, SampleSceneDepth(ub), UNITY_MATRIX_I_VP);
        float3 ab = pa - pb;
        float len = length(ab);
        half near = step(length(pa - positionWS), reach * 4.0) * step(length(pb - positionWS), reach * 4.0);   // not over the model's outline
        half bend = dot(SampleSceneNormals(ua) - SampleSceneNormals(ub), ab / max(len, 1e-5));
        best = max(best, bend * near);
    }
    return smoothstep(0.22, 0.75, best) * fade;
}

void SurfaceFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half4 mask = SampleMetallicSpecGloss(input.uv, 1.0);
    half3 albedo = base.rgb * _BaseColor.rgb;
    half metal = mask.r;
    half smooth = mask.a;
    half occlusion = LerpWhiteTo(mask.g, _OcclusionStrength);

    // the wear, at its own scale: turned a little against the surface's tile, so the two never repeat together
    float2 metres = input.uv / max(abs(_BaseMap_ST.xy), 1e-4);
    float2 wuv = metres / max(_WearTile, 1e-3);
    half4 wear = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, wuv);
    half4 wear2 = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, float2(0.83 * wuv.x - 0.56 * wuv.y, 0.56 * wuv.x + 0.83 * wuv.y) * 0.41 + 0.37);
    float3 normalWS = normalize(input.normalWS);

    // handling: greasy marks dull a shiny thing and shine a dull one
    half prints = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, wuv * 2.9 + 0.19).r; // the size of fingers
    half smudge = saturate(prints * wear2.r * 1.7 + wear2.r * 0.35) * _Smudges;
    smooth = lerp(smooth, lerp(0.62, 0.46, smooth), smudge * 0.75);
    albedo *= 1.0 - 0.07 * smudge;

    // hairlines: bright in the light, through to what is under the paint where they are deep
    half handled = smoothstep(0.3, 0.85, wear2.r * 1.1 + wear2.b * 0.5); // where hands and things get at it, not all over
    half scratch = saturate(wear.g * 1.3 + wear2.g * 0.7) * _Scratches * lerp(0.12, 1.0, handled);
    smooth = lerp(smooth, smooth * 0.72, saturate(scratch * 1.3));
    albedo = lerp(albedo, lerp(albedo, _WearColor.rgb, 0.55), scratch);

    // edges rubbed through
    float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
    half edge = 0.0;
    #if defined(_SCREEN_SPACE_OCCLUSION)                                // (its normals are there only with it)
    if (_EdgeWear > 0.0) edge = OuterEdge(screenUv, input.positionWS);
    #endif
    half rub = saturate(wear2.b * 1.3 + wear.r * 0.6 + wear2.g * 0.5);
    half worn = edge * smoothstep(1.0 - _EdgeWear, 1.25 - 0.6 * _EdgeWear, 0.35 + rub);
    albedo = lerp(albedo, _WearColor.rgb, worn);
    metal = lerp(metal, _WearMetal, worn);
    smooth = lerp(smooth, _WearSmooth, worn);

    // dirt: stains, runs down the sides, what gathers in corners and near the floor
    half side = saturate(1.4 - 1.6 * abs(normalWS.y));
    half dirt = wear2.b * _Stains + (wear.a * 0.8 + wear2.a * 0.5) * side * _Streaks;
    #if defined(_SCREEN_SPACE_OCCLUSION)
    half corner = saturate((1.0 - SampleAmbientOcclusion(screenUv)) * 3.2);
    dirt += corner * _Corners * (0.45 + 0.55 * rub);
    #endif
    float over = frac(input.positionWS.y / max(_FloorHeight, 0.1) + 1e-3) * _FloorHeight;
    half low = saturate(1.0 - over / 0.28);
    dirt += low * low * _FloorDirt * (0.35 + 0.65 * saturate(wear.b + wear2.a + wear.r * 0.5));
    dirt = saturate(dirt) * (1.0 - 0.75 * worn);
    albedo = lerp(albedo, albedo * _DirtColor.rgb, dirt);
    smooth *= 1.0 - 0.55 * dirt;
    metal *= 1.0 - 0.5 * dirt;

    SurfaceData s = (SurfaceData)0;
    s.alpha = 1.0;
    s.albedo = albedo;
    s.metallic = metal;
    s.smoothness = saturate(smooth);
    s.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    s.occlusion = occlusion;

    InputData inputData;
    InitializeInputData(input, s.normalTS, inputData);
    InitializeBakedGIData(input, inputData);
    half4 color = UniversalFragmentPBR(inputData, s);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    outColor = half4(color.rgb, 1.0);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
