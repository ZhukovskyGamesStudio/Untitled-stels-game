// Bounced light, from the screen: every lit surface in view throws a little of its light onto the surfaces that face it —
// the floor under a lamp lights the ceiling and the undersides of the desks, a bright wall tints the cabinet beside it.
// One full-screen pass after the opaque things: for each pixel a spiral of taps round it within a few metres of the world,
// each a small patch (its position from the depth, its facing from the normals, the light it already sends from the
// picture) that gives by how the two face each other and by their distance. Only what is on the screen gives, and only
// what is lit: a dark room stays black. A picture only — the robots see by the lamps (LightSource), not by this.
// Strength: StyleGrade.Bounce (_BounceStrength, VisualStyles.Bounce); 0 skips the pass.
Shader "Stealth/ScreenBounce"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "ScreenBounce"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BounceStrength;
            float _BounceReach; // metres

            #define TAPS 24
            #define GOLDEN 2.39996323

            float3 WorldAt(float2 uv, float depth)
            {
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            bool IsSky(float depth)
            {
            #if UNITY_REVERSED_Z
                return depth < 0.00001;
            #else
                return depth > 0.99999;
            #endif
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 colour = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                float depth = SampleSceneDepth(uv);
                if (_BounceStrength <= 0.001 || IsSky(depth)) return half4(colour, 1);

                float3 p = WorldAt(uv, depth);
                float3 n = SampleSceneNormals(uv);
                float eye = LinearEyeDepth(depth, _ZBufferParams);
                float reach = max(_BounceReach, 0.5);
                // the disc of the world round the point, as it lies on the screen (never the whole screen, never a few pixels)
                float radius = clamp(reach * UNITY_MATRIX_P[1][1] * 0.5 / max(eye, 0.1), 0.05, 0.8);
                float2 aspect = float2(_ScreenParams.y / _ScreenParams.x, 1);
                // the spiral turned from pixel to pixel: its sixteen taps never line up into bands
                float turn = InterleavedGradientNoise(input.positionCS.xy, 0) * 6.2831853;

                float3 sum = 0;
                for (int i = 0; i < TAPS; i++)
                {
                    float a = i * GOLDEN + turn;
                    float2 at = uv + float2(cos(a), sin(a)) * aspect * (radius * sqrt((i + 0.5) / TAPS));
                    if (any(at < 0.002) || any(at > 0.998)) continue;
                    float d = SampleSceneDepth(at);
                    if (IsSky(d)) continue;
                    float3 q = WorldAt(at, d);
                    float3 to = q - p;
                    float dist2 = dot(to, to);
                    float3 dir = to * rsqrt(dist2 + 1e-5);
                    float front = dot(n, dir);
                    if (front <= 0.02 || dist2 >= reach * reach) continue; // behind this surface or out of reach: nothing more to read
                    float3 qn = SampleSceneNormals(at);
                    // how the two patches face each other; a patch turned away gives nothing
                    float facing = front * saturate(dot(qn, -dir));
                    // near things give most, beyond the reach nothing (and nothing from across a gap in depth)
                    float near = saturate(1 - dist2 / (reach * reach)) * reach * reach / (dist2 + 0.15 * reach * reach);
                    float3 light = min(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, at, 0).rgb, 1.0); // a lamp's glass is no sun
                    sum += light * (facing * near);
                }
                sum *= 1.5 / TAPS;

                // what this surface makes of the light that falls on it: its own hue where it is lit, grey where one cannot tell
                float luma = dot(colour, float3(0.299, 0.587, 0.114));
                float3 hue = lerp(float3(1, 1, 1), colour / max(luma, 1e-4), saturate(luma * 12.0));
                float3 albedo = 0.34 * clamp(hue, 0.25, 1.9);
                return half4(colour + sum * albedo * _BounceStrength, 1);
            }
            ENDHLSL
        }
    }
}
