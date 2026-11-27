// What things are made of (AssetBuilder.Surfaces): a tileable PBR surface and its wear — fingerprints, scratches, stains,
// edges rubbed through, dirt in the corners and at the floor — found from the shape on the screen, nothing baked per model.
Shader "Stealth/Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (neutral)", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Relief", Float) = 1
        _MetallicGlossMap("Mask (metal R, occlusion G, smoothness A)", 2D) = "white" {}
        _OcclusionMap("Occlusion (the mask again)", 2D) = "white" {}
        _OcclusionStrength("Occlusion", Range(0, 1)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 1
        _Metallic("Metal (unused: the mask's)", Range(0, 1)) = 0
        _Cutoff("Cutoff (unused)", Range(0, 1)) = 0.5
        _WearMap("Wear (fingerprints R, scratches G, stains B, runs A)", 2D) = "black" {}
        _WearTile("Metres a tile of wear covers", Float) = 1.1
        _Smudges("Fingerprints", Range(0, 1)) = 0.4
        _Scratches("Scratches", Range(0, 1)) = 0.3
        _Stains("Stains", Range(0, 1)) = 0.1
        _Streaks("Runs down the sides", Range(0, 1)) = 0
        _EdgeWear("Edges rubbed through", Range(0, 1)) = 0.35
        _WearColor("Under the surface", Color) = (0.5, 0.5, 0.5, 1)
        _WearMetal("…is metal", Range(0, 1)) = 0
        _WearSmooth("…how smooth", Range(0, 1)) = 0.5
        _Corners("Dirt in corners", Range(0, 1)) = 0.5
        _FloorDirt("Dirt near the floor", Range(0, 1)) = 0.4
        _FloorHeight("From a floor to the next, metres", Float) = 4.5
        _ReliefMap("Relief (soft waves and dents RG, clouds B)", 2D) = "gray" {}
        _ReliefTile("Metres a tile of it covers", Float) = 1.6
        _Relief("Waves and dents", Range(0, 2)) = 0
        _Clouds("Uneven colour and sheen", Range(0, 0.3)) = 0
        _WallBand("Marks where hands and feet reach only (walls)", Range(0, 1)) = 0
        _Contact("Darker at the floor and the ceiling (walls)", Range(0, 1)) = 0
        _RoomHeight("From a floor to its ceiling, metres", Float) = 2.75
        _DirtColor("Dirt", Color) = (0.36, 0.32, 0.27, 1)
        [ToggleOff] _EnvironmentReflections("Mirrors its room", Float) = 1
        // a model's own baked maps over its second UV set (Tools/Blender/shapebake.py; set per renderer by ShapeMaps)
        _ShapeSlice("The model's own maps: their slice in the shared arrays + 1 (0 or less: none)", Float) = 0
        _ShapeUv("…and the UV set they lie over (1 the second, 2 the third)", Float) = 1
        _ShapeInVertices("A mesh merged of many models: each vertex names its maps", Float) = 0
        [HideInInspector] _Cull("__cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300
        HLSLINCLUDE
        #define _NORMALMAP 1
        #define _METALLICSPECGLOSSMAP 1
        #define _OCCLUSIONMAP 1
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include "SurfaceForward.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile _ _WRITE_SMOOTHNESS
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
        // Where each pixel was a frame ago: the temporal smoothing follows what moves (a drawer, a door) instead of smearing it.
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #include "SurfaceInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
