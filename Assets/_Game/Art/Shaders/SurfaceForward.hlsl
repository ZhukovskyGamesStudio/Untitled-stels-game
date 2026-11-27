// The lit pass of Stealth/Surface: URP's lighting of a surface with wear worked into it.
#ifndef STEALTH_SURFACE_FORWARD_INCLUDED
#define STEALTH_SURFACE_FORWARD_INCLUDED

#include "SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

// How much of an outer edge a pixel is (0 a face or a hollow .. 1 a right angle): the normals and places a few millimetres
// to either side, from the normals the ambient occlusion has drawn already — nothing is baked, any shape will do, whether
// whether its bevels are smooth, faceted or missing. Fades out before the edge is as thin as a pixel. A thin rod is not all
// edge: round, it bends twice as much over twice the distance, an edge no more than it did (a stand's tubes, chair legs and
// handles were worn in blotches all over).
half Bend(float2 uv, float2 d, float3 positionWS, float limit)
{
    float2 ua = uv + d, ub = uv - d;
    float3 pa = ComputeWorldSpacePosition(ua, SampleSceneDepth(ua), UNITY_MATRIX_I_VP);
    float3 pb = ComputeWorldSpacePosition(ub, SampleSceneDepth(ub), UNITY_MATRIX_I_VP);
    float3 ab = pa - pb;
    half onModel = step(length(pa - positionWS), limit) * step(length(pb - positionWS), limit);   // not over the model's outline
    return dot(SampleSceneNormals(ua) - SampleSceneNormals(ub), ab / max(length(ab), 1e-5)) * onModel;
}

half OuterEdge(float2 uv, float3 positionWS)
{
    const float reach = 0.0055;
    float away = max(dot(positionWS - GetCameraPositionWS(), -UNITY_MATRIX_V[2].xyz), 0.05);
    float tall = reach * abs(UNITY_MATRIX_P._m11) / (2.0 * away);       // the reach, share of the picture's height
    float px = tall * _ScaledScreenParams.y;
    // Gone before the rubbed strip is a pixel wide (full from two pixels, none under 0.8: about 2 m and 5 m at 1080p). Drawn
    // at a pixel's width it was found in one frame and missed in the next as the view moved: edges of furniture across the
    // room sparkled.
    half fade = saturate((px - 0.8) / 1.2);
    if (fade <= 0.0) return 0.0;
    float2 o = max(px, 1.0) * (_ScaledScreenParams.zw - 1.0);
    half across = Bend(uv, float2(o.x, 0.0), positionWS, reach * 4.0), along = Bend(uv, float2(0.0, o.y), positionWS, reach * 4.0);
    half best = max(across, along);
    if (best <= 0.22) return 0.0;
    half wide = Bend(uv, across >= along ? float2(2.0 * o.x, 0.0) : float2(0.0, 2.0 * o.y), positionWS, reach * 8.0);
    half rod = wide > 0.0 ? smoothstep(1.2, 1.55, wide / best) : 0.45;
    return smoothstep(0.22, 0.75, best) * (1.0 - smoothstep(1.45, 1.6, best)) * (1.0 - rod) * fade;
}

// The lit pass's vertices and, with them, where the model's own baked maps lie: its second UV set and which slice of the
// shared arrays is its own — from the renderer (_ShapeSlice) or, in a mesh merged of many models, from each vertex.
struct SurfaceAttributes
{
    Attributes lit;
    float2 shapeId : TEXCOORD3;         // merged meshes: x slice + 1 (0 none), y ±2 (−2 where the part was mirrored)
};

struct SurfaceVaryings
{
    Varyings lit;
    float4 shape : TEXCOORD12;          // xy the second UV set, z the slice (−1 none), w the frame's sign
};

SurfaceVaryings SurfaceVertex(SurfaceAttributes input)
{
    SurfaceVaryings o;
    o.lit = LitPassVertex(input.lit);
    // Whose maps: a mesh merged of many models says so itself (_ShapeInVertices, set on the merged mesh's renderer) and
    // then each vertex names them; any other mesh has no such data — what is read from a stream a mesh does not have
    // is not zeros (parts of a model picked another model's maps and a mirrored frame) — and is told by its renderer.
    // Nothing names a slice by saying nothing: the number is the slice + 1, so a value that arrives as zero — a block
    // that did not reach the draw, a stream the mesh does not have — means no maps (as −1 it was the first model's:
    // drawers and stall doors, parts with a block of their own, wore the acoustic panel's relief in the build). And a
    // vertex names maps only with its sign of two: a stream of ones is not a name either.
    bool merged = _ShapeInVertices > 0.5;
    bool named = !merged || abs(abs(input.shapeId.y) - 2.0) < 0.25;
    float slice = (named ? (merged ? input.shapeId.x : _ShapeSlice) : 0.0) - 1.0;
    float mirrored = merged ? (input.shapeId.y < 0.0 ? -1.0 : 1.0) : unity_WorldTransformParams.w;
    // (the mesh's second UV set, or its third where the part came with a spare one: nothing here is lightmapped; a
    // merged mesh always carries them as its second)
    float2 own = !merged && _ShapeUv > 1.5 ? input.lit.dynamicLightmapUV : input.lit.staticLightmapUV;
    o.shape = float4(own, slice, mirrored);
    return o;
}

void SurfaceFragment(
    SurfaceVaryings sv
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    Varyings input = sv.lit;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // The model's own maps (nothing where it has none). Their normals are in the tangent space of the second UV set,
    // which no vertex carries: its frame is found from how the place and that UV change across the pixel — right
    // whatever was merged or turned on the way here.
    float2 uv1 = sv.shape.xy;
    float slice = floor(sv.shape.z + 0.5);
    half shapeOn = (slice >= 0.0 ? 1.0 : 0.0) * _ShapesBound * (1.0 - _ShapesOff);
    half3 shape = lerp(half3(1.0, 1.0, 1.0), SAMPLE_TEXTURE2D_ARRAY(_ShapeMasks, sampler_ShapeMasks, uv1, max(slice, 0.0)).rgb, shapeOn);
    half2 shapeXY = SAMPLE_TEXTURE2D_ARRAY(_ShapeNormals, sampler_ShapeNormals, uv1, max(slice, 0.0)).rg * 2.0 - 1.0;
    float3 faceN = normalize(input.normalWS);
    float3 dpx = ddx(input.positionWS), dpy = ddy(input.positionWS);
    float2 dux = ddx(uv1), duy = ddy(uv1);
    float3 perpY = cross(dpy, faceN), perpX = cross(faceN, dpx);
    float3 shapeT = perpY * dux.x + perpX * duy.x, shapeB = perpY * dux.y + perpX * duy.y;
    float shapeScale = rsqrt(max(max(dot(shapeT, shapeT), dot(shapeB, shapeB)), 1e-20));
    // (the sign: our models are imported mirrored in X, their UVs are not, and the bake never flips an island — so the
    // frame found this way is the mirror of the one the map was baked in: without the minus every rib was a slot,
    // checked on a picture; an object mirrored once more by its scale turns it back)
    half3 shapeBend = -(shapeT * shapeXY.x + shapeB * shapeXY.y) * shapeScale * shapeOn * sv.shape.w;

    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half4 mask = SampleMetallicSpecGloss(input.uv, 1.0);
    half3 albedo = base.rgb * _BaseColor.rgb;
    half metal = mask.r;
    half smooth = mask.a;
    half occlusion = LerpWhiteTo(mask.g, _OcclusionStrength) * shape.r;

    // the wear, at its own scale: turned a little against the surface's tile, so the two never repeat together
    float2 metres = input.uv / max(abs(_BaseMap_ST.xy), 1e-4);
    float2 wuv = metres / max(_WearTile, 1e-3);
    half4 wear = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, wuv);
    half4 wear2 = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, float2(0.83 * wuv.x - 0.56 * wuv.y, 0.56 * wuv.x + 0.83 * wuv.y) * 0.41 + 0.37);
    float3 normalWS = normalize(input.normalWS);
    float over = frac(input.positionWS.y / max(_FloorHeight, 0.1) + 1e-3) * _FloorHeight;

    // what a hand's breadth and more is like: slow waves, dents, clouds in the colour and the sheen — it is there from across
    // the room, where the grain has melted away
    float2 ruv = metres / max(_ReliefTile, 1e-3);
    half4 relief = SAMPLE_TEXTURE2D(_ReliefMap, sampler_ReliefMap, ruv);
    half far = SAMPLE_TEXTURE2D(_ReliefMap, sampler_ReliefMap, float2(0.79 * ruv.x - 0.61 * ruv.y, 0.61 * ruv.x + 0.79 * ruv.y) * 0.31 + 0.11).b;
    half clouds = ((relief.b - 0.5) + 1.4 * (far - 0.5)) * 2.0 * _Clouds;
    albedo *= 1.0 + clouds;
    smooth *= 1.0 - 1.2 * clouds;
    half2 waves = (relief.rg * 2.0 - 1.0) * 0.2 * _Relief;

    // a wall is marked where hands, chairs and feet reach
    half band = lerp(1.0, saturate(exp(-pow((over - 0.9) / 0.32, 2.0)) + exp(-pow(over / 0.24, 2.0))), _WallBand);

    // handling: greasy marks dull a shiny thing and shine a dull one
    half prints = SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, wuv * 2.9 + 0.19).r; // the size of fingers
    half smudge = saturate(prints * wear2.r * 1.7 + wear2.r * 0.35) * _Smudges * band;
    smooth = lerp(smooth, lerp(0.62, 0.46, smooth), smudge * 0.75);
    albedo *= 1.0 - 0.07 * smudge;

    // hairlines: bright in the light, through to what is under the paint where they are deep
    half handled = smoothstep(0.3, 0.85, wear2.r * 1.1 + wear2.b * 0.5); // where hands and things get at it, not all over
    half scratch = saturate(wear.g * 1.3 + wear2.g * 0.7) * _Scratches * lerp(0.12, 1.0, handled) * band;
    smooth = lerp(smooth, smooth * 0.72, saturate(scratch * 1.3));
    albedo = lerp(albedo, lerp(albedo, _WearColor.rgb, 0.55), scratch);

    // edges rubbed through
    float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
    half edge = 0.0;
    #if defined(_SCREEN_SPACE_OCCLUSION)                                // (its normals are there only with it)
    if (_EdgeWear > 0.0 && shapeOn < 0.5) edge = OuterEdge(screenUv, input.positionWS);
    #endif
    edge = max(edge, saturate((1.0 - shape.g) * 1.7));                 // the model's own edges: the same from any distance
    half rub = saturate(wear2.b * 1.3 + wear.r * 0.6 + wear2.g * 0.5);
    half worn = edge * smoothstep(0.95 - _EdgeWear, 1.45 - 0.6 * _EdgeWear, 0.35 + rub);
    albedo = lerp(albedo, _WearColor.rgb, worn);
    metal = lerp(metal, _WearMetal, worn);
    smooth = lerp(smooth, _WearSmooth, worn);

    // dirt: stains, runs down the sides, what gathers in corners and near the floor
    half side = saturate(1.4 - 1.6 * abs(normalWS.y));
    half dirt = wear2.b * _Stains + (wear.a * 0.8 + wear2.a * 0.5) * side * _Streaks;
    #if defined(_SCREEN_SPACE_OCCLUSION)
    half corner = saturate((1.0 - SampleAmbientOcclusion(screenUv)) * 2.0);
    dirt += corner * _Corners * (0.45 + 0.55 * rub);
    #endif
    dirt += (1.0 - shape.b) * _Corners * (0.5 + 0.5 * rub);            // the model's own cavities
    half low = saturate(1.0 - over / 0.28);
    dirt += low * low * _FloorDirt * (0.35 + 0.65 * saturate(wear.b + wear2.a + wear.r * 0.5));
    dirt = saturate(dirt) * (1.0 - 0.75 * worn);
    albedo = lerp(albedo, albedo * _DirtColor.rgb, dirt);
    smooth *= 1.0 - 0.55 * dirt;
    metal *= 1.0 - 0.5 * dirt;
    albedo *= lerp(1.0, shape.r, 0.55);                                 // where its parts shut the light out, whatever lamp shines
    albedo *= 1.0 - _Contact * saturate(exp(-over / 0.18) + exp(-max(_RoomHeight - over, 0.0) / 0.22)); // less light gets into a corner

    SurfaceData s = (SurfaceData)0;
    s.alpha = 1.0;
    s.albedo = albedo;
    s.metallic = metal;
    s.smoothness = saturate(smooth);
    s.normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    s.occlusion = occlusion;

    s.normalTS = normalize(half3(s.normalTS.xy + waves, s.normalTS.z));

    InputData inputData;
    InitializeInputData(input, s.normalTS, inputData);
    inputData.normalWS = normalize(inputData.normalWS + shapeBend);
    InitializeBakedGIData(input, inputData);
    half4 color = UniversalFragmentPBR(inputData, s);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    outColor = half4(color.rgb, 1.0);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
