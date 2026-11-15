// A wisp streaming past the camera (plan .docs/plans/cloud-sea.md, step 3; CloudWispsPresenter): a soft streak, drawn
// on a stretched particle, no texture. Coloured with the cloud sea's look (the globals CloudSeaShaderGlobals.ApplyLook
// publishes; white without a sea), lit toward the sun's colour, darkened at night like the other clouds, and seen
// through the air.
Shader "TinCan/Environment/Cloud Wisp"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _Softness ("Softness (how far in the edge fades)", Range(0.05, 1)) = 0.8
        _Lit ("Lit (shadow colour 0 .. lit colour 1)", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/TinCanLighting.hlsl"
            #include "Include/TinCanAtmosphere.hlsl"
            #include "Include/TinCanCloudLook.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Opacity;
                half _Softness;
                half _Lit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 colour : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4 colour : COLOR;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.colour = input.colour;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // A soft round blob on the quad; the particle's stretch makes it a streak.
                float2 p = input.uv * 2.0 - 1.0;
                half alpha = (half)(1.0 - smoothstep(1.0 - _Softness, 1.0, length(p)));
                alpha *= input.colour.a * _Opacity;
                clip(alpha - 0.002h);

                Light sun = GetMainLight();
                half3 lit = _TinCanCloudLookOn > 0.5 ? (half3)_TinCanCloudLookLit.rgb : half3(1, 1, 1);
                half3 shadow = _TinCanCloudLookOn > 0.5 ? (half3)_TinCanCloudLookShadow.rgb : half3(0.85, 0.85, 0.9);
                half3 colour = lerp(shadow, lit * lerp(1.0h, sun.color, 0.3h), _Lit);
                colour = TinCanNightShade(colour);
                return TinCanAtmospherePremultiplied(half4(colour * alpha, alpha), input.positionWS);
            }
            ENDHLSL
        }
    }
}
