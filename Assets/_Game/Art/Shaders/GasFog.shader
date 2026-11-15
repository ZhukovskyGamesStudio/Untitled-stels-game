// The gas outside the house and the sky above it: a full-screen pass before post-processing (so the grading, bloom and
// the style filter apply to it). Where nothing was drawn (the camera clears to black) the sky panorama is added; then
// every pixel is mixed with the gas the view ray crosses on its way (GasCommon.hlsl). Visual only (Stealth.Level.GasView).
Shader "Stealth/GasFog"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "GasFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "GasCommon.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
                if (_GasOn < 0.5) return half4(col, 1);

                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = raw <= 1e-6;
                #else
                    bool sky = raw >= 1.0 - 1e-6;
                #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 eye = _WorldSpaceCameraPos;
                float3 dir = normalize(world - eye);
                float len = sky ? GasSkyDistance() : distance(world, eye);
                if (sky) col += GasSky(dir);

                float tau = GasDepth(eye, dir, len, InterleavedGradientNoise(input.positionCS.xy, 0));
                float t = exp(-tau);
                return half4(col * t + _GasColor.rgb * (1.0 - t), 1);
            }
            ENDHLSL
        }
    }
}
