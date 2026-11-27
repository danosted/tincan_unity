// The ship's spirit: a fiery wisp living in the hearth (plan .docs/plans/spirit-talents.md, "The spirit's look").
// Drawn on one quad that turns about its up axis to face whoever looks at it, with no texture: a teardrop of flame
// whose edge licks and flickers, a warm core, a spirit-cyan base, and two eyes. All of its life is here, slow and calm (seconds per sway, pillar 4), so it needs no
// script and nothing replicates for it: it breathes and sways, blinks now and then, gazes about, and when the viewer
// comes close it perks up (brighter, taller, eyes wide) and looks at them. Colours are palette roles, set by the asset
// builder (ShipDesignsAssetBuilder). Premultiplied, HDR, so bloom takes its glow.
Shader "TinCan/Spirit/Flame Wisp"
{
    Properties
    {
        [HDR] _CoreColor ("Core", Color) = (1.6, 1.4, 1.0, 1)
        [HDR] _BodyColor ("Body", Color) = (1.9, 1.1, 0.4, 1)
        [HDR] _RimColor ("Rim", Color) = (1.3, 0.5, 0.2, 1)
        [HDR] _BaseColor ("Base (spirit)", Color) = (0.6, 1.2, 1.3, 1)
        _EyeColor ("Eyes", Color) = (0.11, 0.09, 0.1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.5
        _Flicker ("Flicker", Range(0, 1)) = 0.6
        _PerkNear ("Perks up within (m)", Float) = 2.5
        _PerkFar ("Dozes beyond (m)", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "DisableBatching"="True" }

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
            #include "Include/TinCanAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _BodyColor;
                half4 _RimColor;
                half4 _BaseColor;
                half4 _EyeColor;
                half _Intensity;
                half _Flicker;
                float _PerkNear;
                float _PerkFar;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                // x: perk (0 dozing .. 1 close), y: seed, z: gaze x, w: gaze y
                float4 state : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return 0.6 * Noise(p) + 0.3 * Noise(p * 2.1 + 7.3) + 0.1 * Noise(p * 4.3 + 1.7);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float seed = Hash(centre.xz * 0.37 + 0.5) * 100.0;
                float t = _Time.y + seed;

                // Perks up as the viewer comes close.
                float distance = length(_WorldSpaceCameraPos - centre);
                float perk = 1.0 - smoothstep(_PerkNear, _PerkFar, distance);

                // Upright billboard: turn about world up to face the camera.
                float3 up = float3(0, 1, 0);
                float3 toCamera = _WorldSpaceCameraPos - centre;
                toCamera.y = 0;
                float3 right = dot(toCamera, toCamera) > 1e-4 ? normalize(cross(up, -toCamera)) : UNITY_MATRIX_V[0].xyz;
                float sx = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                float sy = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));

                // Breathing, a slow bob, and standing taller when it perks up; the base stays on the logs.
                float breathe = 1.0 + 0.04 * sin(t * 0.8) + 0.1 * perk;
                float bob = 0.02 * sin(t * 0.6);
                float2 local = input.positionOS.xy;
                float height = (local.y + 0.5) * breathe - 0.5;
                float3 positionWS = centre + right * local.x * sx * (1.0 + 0.03 * sin(t * 0.7)) + up * (height * sy + bob);

                // Gaze: drifts about while it dozes, settles on the viewer (centre) when it perks up.
                float2 idle = float2(0.6 * sin(t * 0.37) + 0.4 * sin(t * 0.91), 0.5 * sin(t * 0.53 + 1.0));
                float2 gaze = idle * float2(0.09, 0.04) * (1.0 - perk);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.state = float4(perk, seed, gaze);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float perk = input.state.x;
                float t = _Time.y + input.state.y;
                float2 p = input.uv * 2.0 - 1.0;

                // Sway: the flame bends more toward its tip, as if the air moves through it.
                float rise = saturate((p.y + 1.0) * 0.5);
                float sway = (0.08 * sin(p.y * 2.2 - t * 0.9) + 0.025 * sin(p.y * 3.7 - t * 1.4)) * rise * rise;
                float2 q = float2(p.x - sway, p.y);

                // The teardrop: a round belly low down, tapering to a tip that rises and falls.
                const float belly = -0.35;
                const float radius = 0.55;
                float tip = 0.85 + 0.08 * sin(t * 1.0) * _Flicker + 0.08 * perk;
                float f;
                if (q.y < belly)
                {
                    f = length(float2(q.x, (q.y - belly) * 1.1)) / radius;
                }
                else
                {
                    float k = saturate((q.y - belly) / (tip - belly));
                    float width = radius * pow(1.0 - k, 1.3) + 1e-3;
                    f = max(abs(q.x) / width, k > 0.999 ? 2.0 : 0.0);
                }

                // The licking edge: noise rushing upward eats into the outline, more near the tip.
                float licks = Fbm(float2(q.x * 2.4, q.y * 1.8 - t * 0.9));
                f += (licks - 0.45) * (0.15 + 0.35 * rise) * _Flicker;

                half alpha = (half)(1.0 - smoothstep(0.62, 1.0, f));
                clip(alpha - 0.002h);

                // Colour: hot core, golden body, orange rim, darker toward the tip; the base burns spirit cyan.
                half3 colour = lerp((half3)_BodyColor.rgb, (half3)_CoreColor.rgb, (half)smoothstep(0.75, 0.1, f));
                colour = lerp(colour, (half3)_RimColor.rgb, (half)smoothstep(0.55, 0.95, f));
                colour = lerp(colour, (half3)_RimColor.rgb * 0.8h, (half)smoothstep(0.1, 0.9, q.y) * 0.5h);
                colour = lerp(colour, (half3)_BaseColor.rgb, (half)(smoothstep(-0.55, -1.0, q.y) * 0.75));
                half glow = _Intensity * (1.0h + 0.25h * (half)perk) * (half)(0.95 + 0.05 * sin(t * 1.7) * _Flicker);
                colour *= glow;

                // Eyes: two soft ovals in the belly, riding the sway. A blink every few seconds (now and then twice);
                // half-lidded while it dozes, wide when someone is close.
                float period = 4.2 + 1.7 * frac(input.state.y * 0.13);
                float phase = frac(t / period) * period;
                float blink = smoothstep(0.0, 0.07, abs(phase - 0.09)) * smoothstep(0.0, 0.07, abs(phase - (frac(floor(t / period) * 0.61) > 0.7 ? 0.34 : -1.0)));
                float open = blink * lerp(0.5, 1.15, perk);
                float2 eyeCentre = float2(0.0, -0.3) + input.state.zw;
                float eyes = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    float2 e = q - eyeCentre - float2(0.19 * side, 0.0);
                    e.y /= max(open, 0.05) * 1.25;
                    float r = length(e / float2(0.085, 0.1));
                    eyes = max(eyes, 1.0 - smoothstep(0.75, 1.0, r));
                }
                eyes *= step(0.05, open) * saturate(alpha * 2.0);
                colour = lerp(colour, (half3)_EyeColor.rgb, (half)eyes);

                return TinCanAtmospherePremultiplied(half4(colour * alpha, alpha), input.positionWS);
            }
            ENDHLSL
        }
    }
}
