// Anti-aliasing of the final image: a full-screen FXAA pass right after the style filter (StyleScreen). The camera's
// SMAA runs inside post-processing, before the filter, so the filter's ink outlines, hatching and cel edges would stay
// jagged without this pass. Styles made of hard pixels (pixel blocks, 1-bit dither) skip it: their steps are the look.
Shader "Stealth/ScreenFxaa"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "ScreenFxaa"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _StyOn;
            float4 _StyPixel;  // x block size in pixels at 720p
            float4 _StyDither; // x 1-bit on

            float3 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb; }

            // Perceptual brightness (the image is linear: the square root is close enough to gamma here).
            float LumaOf(float3 c) { return sqrt(dot(saturate(c), float3(0.299, 0.587, 0.114))); }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 rgbM = Tap(uv);
                if (_StyOn > 0.5 && (_StyPixel.x > 1.01 || _StyDither.x > 0)) return half4(rgbM, 1);

                float2 px = _BlitTexture_TexelSize.xy;
                float lM = LumaOf(rgbM);
                float lNW = LumaOf(Tap(uv + float2(-1, -1) * px));
                float lNE = LumaOf(Tap(uv + float2(1, -1) * px));
                float lSW = LumaOf(Tap(uv + float2(-1, 1) * px));
                float lSE = LumaOf(Tap(uv + float2(1, 1) * px));
                float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
                float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
                // flat areas stay untouched (no blur of textures)
                if (lMax - lMin < max(0.04, lMax * 0.12)) return half4(rgbM, 1);

                // direction along the edge
                float2 dir;
                dir.x = -((lNW + lNE) - (lSW + lSE));
                dir.y = ((lNW + lSW) - (lNE + lSE));
                float reduce = max((lNW + lNE + lSW + lSE) * 0.25 * 0.125, 1.0 / 128.0);
                float rcpMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
                dir = clamp(dir * rcpMin, -8.0, 8.0) * px;

                float3 rgbA = 0.5 * (Tap(uv + dir * (1.0 / 3.0 - 0.5)) + Tap(uv + dir * (2.0 / 3.0 - 0.5)));
                float3 rgbB = rgbA * 0.5 + 0.25 * (Tap(uv - dir * 0.5) + Tap(uv + dir * 0.5));
                float lB = LumaOf(rgbB);
                return half4(lB < lMin || lB > lMax ? rgbA : rgbB, 1);
            }
            ENDHLSL
        }
    }
}
