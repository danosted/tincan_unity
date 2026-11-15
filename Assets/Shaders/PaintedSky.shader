// The painted sky (ART_DIRECTION.md, "Sky and clouds"; plans .docs/plans/look-slice.md L3 and .docs/plans/day-cycle.md):
// no texture, a gradient from the look's own air at the horizon (TinCanAtmosphere.hlsl's colours, warm toward the sun,
// cool away) up to the zenith; opposite a low sun, the pink band of the belt of Venus above the darker horizon; a sun
// disc with a wide soft halo that takes the sun's colour and strength, so it reddens, dims and goes out as it sets.
// After sunset, procedural stars fixed to the sky, as bright as the day's keys say (_TinCanSkyStars).
// The zenith, the belt and the stars come from SkyShaderGlobals while a day runs; otherwise the material's colours. Below the
// horizon it sinks into a dark void (the bottomless sky under the cloud sea).
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
        _Void ("Void (below the horizon, seen down a hole in the cloud sea)", Color) = (0.12, 0.1, 0.2, 1)
        _VoidDepth ("Void Depth (how far below the horizon it is fully dark, 0..1)", Range(0.05, 1)) = 0.45
        _StarBrightness ("Star Brightness (at night)", Range(0, 4)) = 1.5
        _StarSize ("Star Size (pixels)", Range(0.3, 3)) = 1
        _StarTwinkle ("Star Twinkle", Range(0, 1)) = 0.3
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
                half4 _Void;
                half _VoidDepth;
                half _StarBrightness;
                half _StarSize;
                half _StarTwinkle;
            CBUFFER_END

            float _TinCanSkyOn;
            float4 _TinCanSkyZenith;
            float4 _TinCanSkyBelt;
            float _TinCanSkyStars; // 0 by day .. 1 the full night sky, from the day's keys

            float StarHash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // One layer of stars, fixed to the sky's directions: the directions cut into a lattice of cells (scale cells
            // to the radius), at most one star per cell. A pixel sees only its own cell's star, so a star sits at least
            // 0.2 of a cell from the cell's faces and its light is gone well before them (radius at most 0.06 of a cell);
            // anything wider would be cut square at the cell edge. Each is a soft point a pixel or two across, with its
            // own warmth, brightness and slow twinkle.
            half3 StarLayer(float3 direction, float scale, float chance, float size)
            {
                float3 p = direction * scale;
                float pixel = max(length(fwidth(p)), 1e-5); // before any early return, so the gradient is defined
                float3 cell = floor(p);
                float pick = StarHash(cell);
                if (pick > chance) return 0;

                float3 at = cell + 0.5 + (float3(StarHash(cell + 1.7), StarHash(cell + 2.3), StarHash(cell + 3.1)) - 0.5) * 0.5;
                float3 star = normalize(at) * scale;
                float3 inCell = star - cell;
                if (any(inCell < 0.2) || any(inCell > 0.8)) return 0;

                float radius = min(size * _StarSize * pixel * lerp(0.7, 1.3, StarHash(cell + 4.9)), 0.06);
                float d = distance(p, star);
                half shine = (half)exp(-pow(d / radius, 2.0));
                float own = StarHash(cell + 5.3);
                half bright = (half)lerp(0.25, 1.0, own * own * own);
                half twinkle = 1.0h - _StarTwinkle * (half)(0.5 + 0.5 * sin(_Time.y * lerp(1.5, 4.0, own) + pick * 40.0));
                half3 warmth = lerp(half3(0.75, 0.84, 1.0), half3(1.0, 0.86, 0.68), (half)StarHash(cell + 6.7));
                return warmth * shine * bright * twinkle;
            }

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

                    // The stars after sunset (plan day-cycle.md D4): many faint ones and a few bright, lost in the haze
                    // low down and never below the horizon.
                    if (_TinCanSkyStars > 0.001)
                    {
                        half3 stars = StarLayer(direction, 110.0, 0.4, 1.0) + StarLayer(direction, 38.0, 0.12, 1.7) * 1.6h;
                        colour += stars * (half)(_TinCanSkyStars * smoothstep(0.02, 0.3, direction.y)) * _StarBrightness;
                    }
                }

                float towardSun = dot(direction, toSun);
                colour += _MainLightColor.rgb * (half)pow(saturate(towardSun), _SunHaloFocus) * _SunHalo;
                float discEdge = cos(radians(_SunDisc));
                half disc = (half)smoothstep(discEdge, lerp(discEdge, 1.0, 0.35), towardSun);
                // The disc sinks behind the horizon instead of showing through the ground.
                disc *= (half)smoothstep(-0.01, 0.004, direction.y);
                colour += _MainLightColor.rgb * disc * _SunDiscBrightness;
                // Below the horizon the sky has no bottom (plan .docs/plans/cloud-sea.md): the haze sinks into the void,
                // which is what a hole in the cloud sea shows when looking down it.
                colour = lerp(colour, _Void.rgb, (half)smoothstep(0.02, _VoidDepth, -direction.y));
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
