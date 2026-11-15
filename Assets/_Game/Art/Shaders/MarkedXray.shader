// A robot (or a thing) marked by a teammate, seen through walls as ONE shape: first the pixels where it is in plain view
// are written into the stencil (bit 4: never filled — its own front parts would otherwise "hide" its back ones and the
// fill would cover the visible object), then its hidden parts (mask, bits 1–2), then an outline only outside both, then
// the inside filled exactly once per pixel — overlapping parts never show their seams. Four RenderObjects features on
// PC_Renderer use passes 0–3 on its layer (Detector.XrayLayer, MarkedThings.XrayLayer), the last three with depth test
// Greater (only where a wall is in front).
Shader "Stealth/MarkedXray"
{
    Properties
    {
        _Color ("Fill", Color) = (1, 0.22, 0.14, 0.42)
        _Edge ("Outline", Color) = (1, 0.45, 0.3, 1)
        _Width ("Outline width (px at 720p)", Float) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float4 _Edge;
        float _Width;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; };
        struct Varyings { float4 positionCS : SV_POSITION; };

        Varyings Plain(Attributes v)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
            return o;
        }

        // Pushed outwards on screen, away from the object's origin: every copy of a vertex moves the same way (no cracks
        // at hard edges), the width stays the same in pixels.
        Varyings Inflated(Attributes v)
        {
            Varyings o;
            float4 c = TransformObjectToHClip(v.positionOS.xyz);
            float4 origin = TransformObjectToHClip(float3(0, 0, 0));
            float2 dir = c.xy / c.w - origin.xy / origin.w;
            dir = dir / max(length(dir), 1e-4);
            float px = _Width * _ScreenParams.y / 720.0;
            c.xy += dir * px * 2.0 / _ScreenParams.xy * c.w;
            o.positionCS = c;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "Visible"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            Stencil { Ref 4 WriteMask 4 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Plain
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Greater
            ColorMask 0
            // not where it is in view (bit 4 clear)
            Stencil { Ref 2 ReadMask 4 WriteMask 3 Comp Equal Pass Replace }
            HLSLPROGRAM
            #pragma vertex Plain
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            // Only where nothing is drawn yet (0 < 1: neither in view nor hidden), and only once (it writes 1).
            Stencil { Ref 1 ReadMask 7 Comp Greater Pass Replace }
            HLSLPROGRAM
            #pragma vertex Inflated
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target { return half4(_Edge.rgb * 1.6, _Edge.a); }
            ENDHLSL
        }

        Pass
        {
            Name "Fill"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            // Inside the mask, once per pixel (2 becomes 3).
            Stencil { Ref 2 ReadMask 7 Comp Equal Pass IncrSat }
            HLSLPROGRAM
            #pragma vertex Plain
            #pragma fragment Frag
            half4 Frag(Varyings i) : SV_Target
            {
                // faint scan lines: it reads as "seen by the system", not as a solid object
                float scan = step(0.5, frac(i.positionCS.y * 0.25));
                return half4(_Color.rgb * 1.3, _Color.a * (0.8 + 0.2 * scan));
            }
            ENDHLSL
        }
    }
}
