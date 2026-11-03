// The cloud lab's sky (plan .docs/plans/look-slice.md, "The cloud lab"): a plain gradient from the horizon up to the
// palette's sky, so clouds are judged against a sky rather than a flat colour. The horizon is the look's own air
// (TinCanAtmosphere.hlsl's colours, warm toward the sun, cool away), so far clouds fade into it; without the atmosphere
// it is _Horizon. A stand-in for L3's painted sky, used only in the lab.
Shader "TinCan/Dev/Cloud Lab Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.56, 0.72, 0.85, 1)
        _Horizon ("Horizon (without the atmosphere)", Color) = (0.91, 0.89, 0.82, 1)
        _Falloff ("Falloff (higher keeps the horizon colour lower)", Range(0.1, 4)) = 0.5
        _SunHalo ("Sun Halo", Range(0, 2)) = 0.35
        _SunHaloFocus ("Sun Halo Focus", Range(1, 256)) = 24
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/TinCanAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith;
                half4 _Horizon;
                half _Falloff;
                half _SunHalo;
                half _SunHaloFocus;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.direction = positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half3 horizon = _Horizon.rgb;
                if (_TinCanAtmosphereOn > 0.5)
                {
                    half sunward = (half)pow(saturate(dot(direction, _MainLightPosition.xyz) * 0.5 + 0.5), _TinCanFogShape.y);
                    horizon = lerp(_TinCanAwayFog.rgb, _TinCanSunwardFog.rgb, sunward);
                }

                half up = (half)pow(saturate(direction.y), _Falloff);
                half3 colour = lerp(horizon, _Zenith.rgb, up);
                colour += _MainLightColor.rgb * pow(saturate(dot(direction, _MainLightPosition.xyz)), _SunHaloFocus) * _SunHalo;
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
