// The whole-screen look of a visual style (Stealth.Level.VisualStyles sets the _Sty* globals). Runs after URP
// post-processing through a FullScreenPassRendererFeature on PC_Renderer; everything is off by default (_StyOn = 0).
// Order: pixel blocks → painterly filter → cel bands / colour levels → hatching → outlines / neon edges → 1-bit dither
// → paper → camcorder (chroma, scanlines, noise, REC).
Shader "Stealth/StyleScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "StyleScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _StyOn;
            float4 _StyPixel;        // x block size in pixels at 720p
            float4 _StyCel;          // x luminance bands (0 off), y levels per channel (0 off), z band softness, w how much of the bands
            float4 _StyOutline;      // x strength, y width px, z depth sensitivity, w normal sensitivity
            float4 _StyOutlineColor; // rgb ink, a: 0 = ink colour, 1 = darkened surface colour
            float4 _StyPaint;        // x painterly (Kuwahara) radius px
            float4 _StyHatch;        // x strength, y spacing px, z darkness where hatching starts
            float4 _StyPaper;        // x grain, y tint strength
            float4 _StyPaperColor;
            float4 _StyDither;       // x 1-bit on, y dot size px, z bias, w ordered dither for colour levels
            float4 _StyInk;          // 1-bit dark colour
            float4 _StyLight;        // 1-bit light colour
            float4 _StyVhs;          // x scanlines, y chroma shift px, z noise, w wobble
            float4 _StyRec;          // x REC dot
            float4 _StyGlow;         // x neon edge strength, y how much of the image remains, z far distance of the gradient
            float4 _StyGlowA;        // neon colour near
            float4 _StyGlowB;        // neon colour far
            float4 _StyExtra;        // x inverted lines, y grey world but the lights, z helmet visor, w viewfinder brackets

            float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 8×8 ordered (Bayer) threshold in 0..1.
            static const float kBayer[64] = {
                 0, 32,  8, 40,  2, 34, 10, 42, 48, 16, 56, 24, 50, 18, 58, 26,
                12, 44,  4, 36, 14, 46,  6, 38, 60, 28, 52, 20, 62, 30, 54, 22,
                 3, 35, 11, 43,  1, 33,  9, 41, 51, 19, 59, 27, 49, 17, 57, 25,
                15, 47,  7, 39, 13, 45,  5, 37, 63, 31, 55, 23, 61, 29, 53, 21 };
            float Bayer(uint2 p) { p &= 7; return (kBayer[p.y * 8 + p.x] + 0.5) / 64.0; }

            float3 Fetch(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb; }

            float EyeDepth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            // Edges from depth (relative jumps) and normals (creases).
            float Edge(float2 uv, float2 step)
            {
                float d = EyeDepth(uv);
                float3 n = SampleSceneNormals(uv);
                float de = 0, ne = 0;
                float2 offs[4] = { float2(step.x, 0), float2(-step.x, 0), float2(0, step.y), float2(0, -step.y) };
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 q = uv + offs[i];
                    de += abs(EyeDepth(q) - d);
                    ne += 1 - saturate(dot(SampleSceneNormals(q), n));
                }
                float depthEdge = saturate((de / max(d, 0.1) - 0.08) * _StyOutline.z);
                float normalEdge = saturate((ne - 0.35) * _StyOutline.w);
                // Far away lines fade out (the gas and the haze swallow them).
                return max(depthEdge, normalEdge) * saturate(1.5 - d / 40.0);
            }

            // Painterly: the flattest of the four quadrants around the pixel (Kuwahara).
            float3 Paint(float2 uv, float2 px, int r)
            {
                float3 mean[4]; float var[4];
                int2 q0[4] = { int2(-1, -1), int2(1, -1), int2(-1, 1), int2(1, 1) };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float3 s = 0, s2 = 0; float n = 0;
                    [loop] for (int y = 0; y <= r; y++)
                        [loop] for (int x = 0; x <= r; x++)
                        {
                            float3 c = Fetch(uv + float2(x * q0[k].x, y * q0[k].y) * px);
                            s += c; s2 += c * c; n += 1;
                        }
                    mean[k] = s / n;
                    float3 v = s2 / n - mean[k] * mean[k];
                    var[k] = v.r + v.g + v.b;
                }
                float3 best = mean[0]; float bv = var[0];
                [unroll] for (int j = 1; j < 4; j++) if (var[j] < bv) { bv = var[j]; best = mean[j]; }
                return best;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 src = Fetch(uv);
                if (_StyOn < 0.5) return half4(src, 1);

                float2 res = _BlitTexture_TexelSize.zw;
                float scale = res.y / 720.0;
                float2 px = _BlitTexture_TexelSize.xy;

                // Camcorder wobble: lines drift sideways a little.
                if (_StyVhs.w > 0)
                    uv.x += (sin(uv.y * 40 + _Time.y * 3) * 0.6 + sin(uv.y * 7 - _Time.y * 1.3)) * _StyVhs.w * px.x * 2;

                // Pixel blocks: everything (colour, depth, normals) is read at the centre of the block.
                float block = max(1, _StyPixel.x * scale);
                float2 cellPx = block * px;
                float2 suv = block > 1.01 ? (floor(uv / cellPx) + 0.5) * cellPx : uv;
                uint2 cell = (uint2)floor(uv * res / max(block, 1));

                float3 c = _StyPaint.x > 0 ? Paint(suv, px * max(1, scale), (int)_StyPaint.x) : Fetch(suv);

                // Camcorder chroma: red and blue slip apart.
                if (_StyVhs.y > 0)
                {
                    float2 sh = float2(_StyVhs.y * scale * px.x, 0);
                    c.r = Fetch(suv + sh).r;
                    c.b = Fetch(suv - sh).b;
                }

                float3 g = LinearToSRGB(saturate(c));

                // Grey world: only bright things (lamps, lasers, screens) keep their colour.
                if (_StyExtra.y > 0)
                {
                    float keep = smoothstep(0.55, 0.9, max(g.r, max(g.g, g.b)));
                    g = lerp(g, lerp(Luma(g).xxx, g, keep), _StyExtra.y);
                }

                // Cel bands: light in a few flat steps, the colour kept.
                if (_StyCel.x > 0)
                {
                    float l = Luma(g);
                    float b = l * _StyCel.x;
                    float stepped = (floor(b) + smoothstep(0.5 - _StyCel.z, 0.5 + _StyCel.z, frac(b))) / _StyCel.x;
                    // The darkest band keeps a little of the real light (no black holes around lamps).
                    stepped = max(stepped, l * 0.5);
                    g = lerp(g, saturate(g * stepped / max(l, 0.002)), _StyCel.w);
                }
                // Few colours per channel (PS1 / VGA), with an ordered dither to keep gradients.
                if (_StyCel.y > 0)
                {
                    float d = _StyDither.w > 0 ? Bayer(cell) - 0.5 : 0;
                    g = floor(g * _StyCel.y + 0.5 + d) / _StyCel.y;
                }

                // Hatching in the shadows: diagonal lines, crossed where it is darker still.
                if (_StyHatch.x > 0)
                {
                    float l = Luma(g);
                    float2 p = uv * res / max(1, _StyHatch.y * scale);
                    float a = abs(frac(p.x + p.y) - 0.5);
                    float b = abs(frac(p.x - p.y) - 0.5);
                    float line1 = smoothstep(0.18, 0.08, a) * smoothstep(_StyHatch.z, _StyHatch.z * 0.5, l);
                    float line2 = smoothstep(0.18, 0.08, b) * smoothstep(_StyHatch.z * 0.5, _StyHatch.z * 0.2, l);
                    g = lerp(g, _StyOutlineColor.rgb * 0.6, saturate(max(line1, line2)) * _StyHatch.x);
                }

                // Ink outlines (or neon edges).
                if (_StyOutline.x > 0 || _StyGlow.x > 0)
                {
                    float2 step = max(1, _StyOutline.y * scale) * px;
                    if (block > 1.01) step = cellPx;
                    float e = Edge(suv, step);
                    if (_StyGlow.x > 0)
                    {
                        float halo = Edge(suv, step * 3) * 0.45;
                        float far = saturate(EyeDepth(suv) / max(_StyGlow.z, 0.1));
                        float3 neon = lerp(_StyGlowA.rgb, _StyGlowB.rgb, far);
                        // glowing things (lasers, screens, lamps) stay on screen under the neon
                        float keepK = lerp(_StyGlow.y, 1, smoothstep(0.5, 0.85, max(g.r, max(g.g, g.b))));
                        g = g * keepK + neon * saturate(e + halo) * _StyGlow.x;
                    }
                    else
                    {
                        float3 ink = lerp(_StyOutlineColor.rgb, g * 0.25, _StyOutlineColor.a);
                        if (_StyExtra.x > 0.5) ink = Luma(g) < 0.35 ? float3(1, 1, 1) : float3(0, 0, 0);
                        g = lerp(g, ink, e * _StyOutline.x);
                    }
                }

                // 1-bit: two inks, ordered dither of the brightness.
                if (_StyDither.x > 0)
                {
                    uint2 dp = (uint2)floor(uv * res / max(1, _StyDither.y * scale));
                    float l = saturate(Luma(g) + _StyDither.z);
                    float hi = max(g.r, max(g.g, g.b)), sat = hi - min(g.r, min(g.g, g.b));
                    float3 bit = l > Bayer(dp) ? _StyLight.rgb : _StyInk.rgb;
                    // danger and loot stay in colour: strongly saturated bright things (lasers, items, alarm lights)
                    g = sat > 0.45 && hi > 0.55 ? g : bit;
                }

                // Paper: a tint and a grain that stays in place.
                if (_StyPaper.x > 0 || _StyPaper.y > 0)
                {
                    float2 p = uv * res / max(1, scale);
                    float grain = Hash(floor(p)) * 0.6 + Hash(floor(p / 4)) * 0.4;
                    g = lerp(g, g * _StyPaperColor.rgb, _StyPaper.y);
                    g *= 1 - _StyPaper.x * (grain - 0.5);
                }

                // Camcorder: scanlines, noise, a tracking band, REC.
                if (_StyVhs.x > 0 || _StyVhs.z > 0)
                {
                    float y = uv.y * res.y / max(1, scale);
                    g *= 1 - _StyVhs.x * (0.5 + 0.5 * sin(y * 3.14159));
                    float n = Hash(floor(uv * res / max(1, scale * 1.5)) + frac(_Time.y * 13.1) * 91.7) - 0.5;
                    g += n * _StyVhs.z;
                    float band = smoothstep(0.03, 0, abs(frac(uv.y * 0.6 - _Time.y * 0.07) - 0.5));
                    g += band * _StyVhs.z * 0.6;
                }
                if (_StyRec.x > 0)
                {
                    float2 dotPos = float2(0.055, 0.92);
                    float2 d = (uv - dotPos) * float2(res.x / res.y, 1);
                    float on = step(0.5, frac(_Time.y * 0.5));
                    g = lerp(g, float3(0.95, 0.08, 0.06), smoothstep(0.016, 0.013, length(d)) * on);
                }

                // Helmet visor: dark rims curving over the top and the bottom corners.
                if (_StyExtra.z > 0)
                {
                    float cx = (uv.x - 0.5) * res.x / res.y;
                    float top = smoothstep(-0.008, 0.008, uv.y - (0.955 - 0.09 * cx * cx));
                    float bottom = smoothstep(0.008, -0.008, uv.y - (0.0 + 0.16 * cx * cx - 0.02));
                    g = lerp(g, float3(0.02, 0.02, 0.025), saturate(top + bottom) * _StyExtra.z);
                }
                // Viewfinder: four corner brackets.
                if (_StyExtra.w > 0)
                {
                    float2 q = abs(uv - 0.5);
                    float2 pxq = (0.5 - q) * res / scale;      // pixels (at 720p) from the edges
                    float2 inset = float2(56, 44);
                    float2 d = pxq - inset;
                    bool corner = d.x > -1.5 && d.y > -1.5 && d.x < 70 && d.y < 70;
                    bool lineH = abs(d.y) < 1.5 && d.x < 70 && d.x > -1.5;
                    bool lineV = abs(d.x) < 1.5 && d.y < 70 && d.y > -1.5;
                    if (corner && (lineH || lineV)) g = lerp(g, float3(0.95, 0.95, 0.92), 0.9 * _StyExtra.w);
                }

                return half4(SRGBToLinear(saturate(g)), 1);
            }
            ENDHLSL
        }
    }
}
