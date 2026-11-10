// A robot marked by a teammate, seen through walls: only the hidden parts (ZTest Greater) are drawn, as a red glow that
// is brighter at the silhouette's edge. Used as the override material of the "MarkedXray" RenderObjects feature on
// PC_Renderer, which draws the Marked layer (Detector.XrayLayer) after the transparent objects.
Shader "Stealth/MarkedXray"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.2, 0.12, 1)
        _Fill ("Fill", Range(0, 1)) = 0.28
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "MarkedXray"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Greater
            Cull Back
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Fill;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float rim = 1.0 - saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS))));
                float a = saturate(_Fill + pow(rim, 2.0) * 0.9);
                return half4(_Color.rgb * 1.6, a * _Color.a);
            }
            ENDHLSL
        }
    }
}
