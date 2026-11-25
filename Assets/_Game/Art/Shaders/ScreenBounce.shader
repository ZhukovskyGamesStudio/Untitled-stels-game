// Bounced light, from the screen: every lit surface in view throws a little of its light onto the surfaces that face it —
// the floor under a lamp lights the ceiling and the undersides of the desks, a bright wall tints the cabinet beside it.
// One full-screen pass after the opaque things: for each pixel a spiral of taps round it within a few metres of the world,
// each a small patch (its position from the depth, its facing from the normals, the light it already sends from the
// picture) that gives by how the two face each other and by their distance. Only what is on the screen gives, and only
// what is lit: a dark room stays black. A picture only — the robots see by the lamps (LightSource), not by this.
// Strength: StyleGrade.Bounce (_BounceStrength, VisualStyles.Bounce); 0 skips it. It is the close-up half of the bounced
// light — the underside of a desk over a lit floor. The other half is the room fill (RoomFill.cs, RoomFillField): the
// level a room's own burning lamps leave in it, read from the plan's cells by where the surface is and which way it
// faces — steady, the same from every side, and shut in by the room's walls (a cell's neighbour counts only where
// nothing stands between them). Strength: StyleGrade.RoomFill (_RoomFillStrength).
// Contact shadows (ContactShadows.cs) ride the same pass: from every pixel a short walk towards each of the nearest
// ceiling lamps over the screen's depth; where something stands in the way close by — a box on a desk, too thin for the
// lamp's shadow map — the pixel loses that lamp's share of its light. No noise: the same steps for every pixel.
// Strength: StyleGrade.Contact (_ContactStrength).
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            float _BounceStrength;
            float _BounceReach; // metres

            StructuredBuffer<uint> _RoomFillCells; // RoomFill.Pack: two shut sides, open above, the level
            float4 _RoomFillGrid;    // cells along x, along z, floors, cells per metre
            float4 _RoomFillHeights; // floor to floor, a room's height, the step off the surface, the brightest level
            float4 _RoomFillOrigin;  // the plan's corner in the world (x, z)
            float _RoomFillStrength;

            #define FILL_BLOCK_X 0x80000000u
            #define FILL_BLOCK_Z 0x40000000u
            #define FILL_OPEN_UP 0x20000000u

            bool FillInside(int f, int2 c)
            {
                return f >= 0 && f < (int)_RoomFillGrid.z && c.x >= 0 && c.y >= 0 && c.x < (int)_RoomFillGrid.x && c.y < (int)_RoomFillGrid.y;
            }

            uint FillCell(int f, int2 c)
            {
                return _RoomFillCells[(f * (int)_RoomFillGrid.y + c.y) * (int)_RoomFillGrid.x + c.x];
            }

            float3 FillLevel(uint cell)
            {
                float3 v = float3((cell >> 19) & 1023u, (cell >> 9) & 1023u, cell & 511u) / float3(1023.0, 1023.0, 511.0);
                return v * v * _RoomFillHeights.w;
            }

            // nothing stands between a cell and its neighbour one step along x (or z)
            bool FillOpen(int f, int2 c, int2 step)
            {
                int2 to = c + step;
                if (!FillInside(f, c) || !FillInside(f, to)) return false;
                int2 low = min(c, to);
                return (FillCell(f, low) & (step.x != 0 ? FILL_BLOCK_X : FILL_BLOCK_Z)) == 0u;
            }

            // RoomFill.Grid.Sample, step for step
            float3 FillAt(float3 p, float3 n)
            {
                float3 q = p + n * _RoomFillHeights.z;
                int f = (int)floor(q.y / _RoomFillHeights.x);
                float2 g = (q.xz - _RoomFillOrigin.xy) * _RoomFillGrid.w;
                int2 home = (int2)floor(g);
                if (!FillInside(f, home)) return 0;
                uint cell = FillCell(f, home);
                if (q.y - f * _RoomFillHeights.x > _RoomFillHeights.y && (cell & FILL_OPEN_UP) == 0u) return 0; // in the slab: the ducts
                float2 t = g - home - 0.5;
                int2 s = int2(t.x >= 0 ? 1 : -1, t.y >= 0 ? 1 : -1);
                t = abs(t);
                bool openX = FillOpen(f, home, int2(s.x, 0)), openZ = FillOpen(f, home, int2(0, s.y));
                bool openD = (openX && FillOpen(f, home + int2(s.x, 0), int2(0, s.y))) || (openZ && FillOpen(f, home + int2(0, s.y), int2(s.x, 0)));
                float3 c = FillLevel(cell);
                float3 cx = c, cz = c;
                if (openX) cx = FillLevel(FillCell(f, home + int2(s.x, 0)));
                if (openZ) cz = FillLevel(FillCell(f, home + int2(0, s.y)));
                float3 cd = (cx + cz) * 0.5;
                if (openD) cd = FillLevel(FillCell(f, home + s));
                return lerp(lerp(c, cx, t.x), lerp(cz, cd, t.x), t.y);
            }

            #define CONTACT_LAMPS 8
            #define CONTACT_STEPS 12
            #define CONTACT_REACH 0.12
            #define CONTACT_THICK 0.06
            float4 _ContactLamps[CONTACT_LAMPS]; // where a lamp hangs (world), how bright it burns
            int _ContactCount;
            float _ContactStrength;

            // the share of the lamps' light on this point that something close by stands in the way of
            float ContactShade(float3 p, float3 n, float eye)
            {
                float lit = 1e-4, shaded = 0;
                float3 from = p + n * (0.012 + 0.002 * eye);
                float bias = 0.008 + 0.004 * eye;
                for (int i = 0; i < _ContactCount && i < CONTACT_LAMPS; i++)
                {
                    float3 to = _ContactLamps[i].xyz - p;
                    if (to.y < 0.05 || to.y > 3.4) continue; // a lamp of another storey
                    float dist = length(to);
                    float3 dir = to / dist;
                    float facing = dot(n, dir);
                    if (facing <= 0.05) continue;
                    float weight = _ContactLamps[i].w * facing / (dist * dist + 1.0);
                    lit += weight;
                    // (a lamp that only skims the surface: its ray runs along it and hits the surface itself)
                    if (facing <= 0.3) continue;
                    float reach = min(CONTACT_REACH, dist * 0.8);
                    float hit = 0;
                    for (int k = 1; k <= CONTACT_STEPS; k++)
                    {
                        float t = (float)k / CONTACT_STEPS;
                        float3 s = from + dir * (reach * t * t); // (denser near the surface: thin things)
                        float4 clip = TransformWorldToHClip(s);
                        if (clip.w <= 0.05) break;
                        float2 at = ComputeNormalizedDeviceCoordinates(s, UNITY_MATRIX_VP);
                        if (any(at < 0.001) || any(at > 0.999)) break;
                        float behind = clip.w - LinearEyeDepth(SampleSceneDepth(at), _ZBufferParams);
                        // (only what stands right there: a thing a hand's width in front of the ray is in front of it, not on it)
                        if (behind > bias && behind < CONTACT_THICK) { float f = 1.0 - t; hit = f * f; break; } // (nearer things shade harder, at the reach nothing)
                    }
                    shaded += weight * hit;
                }
                return shaded / lit;
            }

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
                bool fills = _RoomFillStrength > 0.001 && _RoomFillGrid.z > 0.5;
                bool contact = _ContactStrength > 0.001 && _ContactCount > 0;
                if ((_BounceStrength <= 0.001 && !fills && !contact) || IsSky(depth)) return half4(colour, 1);

                float3 p = WorldAt(uv, depth);
                float3 n = SampleSceneNormals(uv);
                if (contact)
                {
                    // what glows of itself (a lamp's glass, a screen) is not lit by the lamps: nothing to take away
                    float own = saturate((1.6 - dot(colour, float3(0.299, 0.587, 0.114))) * 2.5);
                    colour *= 1.0 - _ContactStrength * own * ContactShade(p, n, LinearEyeDepth(depth, _ZBufferParams));
                }
                // what this surface makes of the light that falls on it: its own hue where it is lit, grey where one cannot tell
                float luma = dot(colour, float3(0.299, 0.587, 0.114));
                float3 hue = lerp(float3(1, 1, 1), colour / max(luma, 1e-4), saturate(luma * 12.0));
                float3 albedo = 0.34 * clamp(hue, 0.25, 1.9);

                float3 fill = 0;
                if (fills)
                {
                    // the room's level: a little more on what faces down (the lit floor is the biggest lamp), less in the corners
                    fill = FillAt(p, n) * (1.0 - 0.18 * n.y) * _RoomFillStrength;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    fill *= SampleAmbientOcclusion(uv);
                #endif
                }
                if (_BounceStrength <= 0.001) return half4(colour + fill * albedo, 1);
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
                    float3 light = min(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, at, 0).rgb, 0.6); // a lamp's glass is no sun (a few taps on something that glows are grain)
                    sum += light * (facing * near);
                }
                sum *= 1.5 / TAPS;

                return half4(colour + (sum * _BounceStrength + fill) * albedo, 1);
            }
            ENDHLSL
        }
    }
}
