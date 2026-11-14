// The painted sky (ART_DIRECTION.md, "Sky and clouds"; plans .docs/plans/look-slice.md L3 and .docs/plans/day-cycle.md):
// no texture, a gradient from the look's own air at the horizon (TinCanAtmosphere.hlsl's colours, warm toward the sun,
// cool away) up to the zenith; opposite a low sun, the pink band of the belt of Venus above the darker horizon; a sun
// disc with a wide soft halo that takes the sun's colour and strength, so it reddens, dims and goes out as it sets.
// The zenith and the belt come from SkyShaderGlobals while a day runs; otherwise the material's colours.
Shader "TinCan/Environment/Painted Sky"
{
    Properties
    {
        _Zenith ("Zenith (without a day)", Color) = (0.56, 0.72, 0.85, 1)
        _Horizon ("Horizon (without the atmosphere)", Color) = (0.91, 0.89, 0.82, 1)
        _Falloff ("Falloff (higher keeps the horizon colour lower)", Range(0.1, 4)) = 0.5
        _SunHalo ("Sun Halo", Range(0, 2)) = 0.35
        _SunHaloFocus ("Sun Halo Focus", Range(1, 256)) = 24
        _SunDisc ("Sun Disc Size (degrees)", Range(0.2, 5)) = 1.4
        _SunDiscBrightness ("Sun Disc Brightness", Range(0, 8)) = 2.5
        _BeltHeight ("Belt Height (up, 0..1)", Range(0, 0.5)) = 0.1
        _BeltWidth ("Belt Width", Range(0.01, 0.4)) = 0.09
        _BeltFocus ("Belt Focus (higher keeps it opposite the sun)", Range(0.5, 8)) = 2
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
                half _SunDisc;
                half _SunDiscBrightness;
                half _BeltHeight;
                half _BeltWidth;
                half _BeltFocus;
            CBUFFER_END

            float _TinCanSkyOn;
            float4 _TinCanSkyZenith;
            float4 _TinCanSkyBelt;

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
                float3 toSun = _MainLightPosition.xyz;
                bool day = _TinCanSkyOn > 0.5;

                half3 horizon = _Horizon.rgb;
                if (_TinCanAtmosphereOn > 0.5)
                {
                    half sunward = (half)pow(saturate(dot(direction, toSun) * 0.5 + 0.5), _TinCanFogShape.y);
                    horizon = lerp(_TinCanAwayFog.rgb, _TinCanSunwardFog.rgb, sunward);
                }

                half3 zenith = day ? (half3)_TinCanSkyZenith.rgb : _Zenith.rgb;
                half up = (half)pow(saturate(direction.y), _Falloff);
                half3 colour = lerp(horizon, zenith, up);

                if (day)
                {
                    // The belt of Venus: a band a little above the horizon, opposite the sun along the ground.
                    float2 flatSun = toSun.xz / max(length(toSun.xz), 1e-4);
                    float2 flatView = direction.xz / max(length(direction.xz), 1e-4);
                    half away = (half)pow(saturate(dot(flatView, -flatSun) * 0.5 + 0.5), _BeltFocus);
                    half band = (half)exp(-pow((direction.y - _BeltHeight) / _BeltWidth, 2.0));
                    colour = lerp(colour, (half3)_TinCanSkyBelt.rgb, saturate((half)_TinCanSkyBelt.a * away * band));
                }

                float towardSun = dot(direction, toSun);
                colour += _MainLightColor.rgb * (half)pow(saturate(towardSun), _SunHaloFocus) * _SunHalo;
                float discEdge = cos(radians(_SunDisc));
                half disc = (half)smoothstep(discEdge, lerp(discEdge, 1.0, 0.35), towardSun);
                // The disc sinks behind the horizon instead of showing through the ground.
                disc *= (half)smoothstep(-0.01, 0.004, direction.y);
                colour += _MainLightColor.rgb * disc * _SunDiscBrightness;
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
