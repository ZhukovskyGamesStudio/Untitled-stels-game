// A lampshade lit from inside: URP's lit surface whose own glow is not one flat colour but falls off from the bulb —
// bright where the cloth is nearest to it, dim at the far rim, brighter where one looks straight through the cloth and
// on the inside, which the bulb lights directly. Where the bulb is comes per renderer (ShadeGlow, a property block);
// without it the glow is flat, as before. The glow's colour is the material's emission (dim decor: LightLeakTests).
Shader "Stealth/Shade"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _Cutoff("Cutoff (unused)", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.1
        _Metallic("Metal", Range(0, 1)) = 0
        _BumpScale("Relief (unused)", Float) = 1
        _OcclusionStrength("Occlusion (unused)", Range(0, 1)) = 1
        [HDR] _EmissionColor("Glow", Color) = (0, 0, 0)
        _InnerPeak("Glow nearest the bulb, times", Range(1, 6)) = 2.6
        _InnerFloor("Glow farthest from it, times", Range(0, 1)) = 0.35
        [HideInInspector] _Cull("__cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex LitPassVertex
            #pragma fragment ShadeFragment
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #define _EMISSION 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            // (per renderer, outside the material's block: ShadeGlow)
            float4 _InnerAt;   // the bulb in the world; w — the nearest the shade comes to it, metres
            float _InnerSpan;  // from that to the shade's farthest point, metres; 0 — no bulb known: a flat glow
            float _InnerPeak, _InnerFloor;

            void ShadeFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData s;
                InitializeStandardLitSurfaceData(input.uv, s);
                InputData inputData;
                InitializeInputData(input, s.normalTS, inputData);
                InitializeBakedGIData(input, inputData);

                half glow = 1.0;
                if (_InnerSpan > 1e-4)
                {
                    float3 to = _InnerAt.xyz - inputData.positionWS;
                    float far = length(to);
                    half t = saturate((far - _InnerAt.w) / _InnerSpan);
                    glow = lerp(_InnerPeak, _InnerFloor, smoothstep(0.0, 1.0, t));
                    // looked at straight through, the cloth passes more than along its edge
                    glow *= lerp(0.6, 1.0, saturate(dot(inputData.normalWS, inputData.viewDirectionWS)));
                    // the inside faces the bulb: lit by it directly
                    glow *= 1.0 + 0.8 * saturate(dot(inputData.normalWS, to / max(far, 1e-4)));
                }
                s.emission = _EmissionColor.rgb * glow;

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                outColor = half4(color.rgb, 1.0);

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
