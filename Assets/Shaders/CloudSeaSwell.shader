// The drawn cloud sea (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"; ART_DIRECTION.md, "Sky and
// clouds"): a flat grid around the camera (CloudSeaPresenter) lifted by the shared swell and sunk into the server's
// holes, with the formula the game reads (Include/TinCanCloudSea.hlsl). On top of that, visual only:
// - Lumps: billow noise heaps the surface into rounded mounds with creases between them, at two sizes, slowly
//   morphing, so the crests break into cauliflower edges instead of reading as smooth hills.
// - Shells: the same grid drawn again in thin see-through layers above the surface (_ShellLayer 1..N, set by the
//   presenter), each cut by one drifting fluff field at a rising threshold, so the tops are soft and fray, light
//   passes through thin edges, and you see a little way into the cloud. Layer 0 is the surface itself.
// - Fog over ground: every layer thins where the ground lies just behind it (depth texture, _FogDepth), so where the
//   sea rises over land it reads as fog instead of contour lines.
// Shaded like the soft clouds, not like water (Include/TinCanCloudLook.hlsl, shared with the volumetric sky): cream lit
// heaps, cool hollows, a sky-coloured rim at grazing angles, light through the thin tops toward the sun. A hole's rim rolls down into it, darkens toward the void and dissolves;
// a raised lip rings it. None of the visual detail reaches the game.
Shader "TinCan/Environment/Cloud Sea Swell"
{
    Properties
    {
        [Header(Colour)]
        _LitColour ("Lit", Color) = (0.984, 0.965, 0.925, 1)
        _ShadowColour ("Shadow", Color) = (0.794, 0.767, 0.767, 1)
        _HollowColour ("Hollow (between heaps and in troughs)", Color) = (0.6, 0.6, 0.76, 1)
        _HollowDarken ("Hollow Darken", Range(0, 1)) = 0.55
        _SunTint ("Sun Tint", Range(0, 1)) = 0.5
        _Ambient ("Ambient (sky fill in shadow)", Range(0, 1)) = 0.35

        [Header(Light)]
        _Wrap ("Wrap", Range(0, 1)) = 0.3
        _BandSoftness ("Band Softness", Range(0.01, 1)) = 0.7
        _Bands ("Bands (2 or 3)", Range(2, 3)) = 2
        _Rim ("Rim (sky at grazing angles)", Range(0, 2)) = 0.35
        _RimPower ("Rim Power", Range(1, 8)) = 3
        _SilverLining ("Silver Lining (looking into the sun)", Range(0, 4)) = 0.8
        _SilverFocus ("Silver Lining Focus", Range(1, 32)) = 6
        _Translucency ("Translucency (sun through thin tops)", Range(0, 2)) = 0.6

        [Header(Lumps)]
        _LumpScale ("Big Heaps (m)", Float) = 70
        _LumpHeight ("Big Heap Height (m)", Float) = 12
        _LumpScale2 ("Small Heaps (m)", Float) = 26
        _LumpHeight2 ("Small Heap Height (m)", Float) = 4
        _LumpMorph ("Morph (per s)", Float) = 0.01
        _BillowScale ("Fine Billows (m)", Float) = 14
        _BillowStrength ("Fine Billow Strength (in the light)", Range(0, 2)) = 0.4

        [Header(Shells)]
        _FluffScale ("Fluff Size (m)", Float) = 22
        _FluffSoftness ("Fluff Softness", Range(0.02, 1)) = 0.3
        _FluffMorph ("Fluff Morph (per s)", Float) = 0.03
        _ShellOpacity ("Shell Opacity", Range(0, 1)) = 0.45
        _ShellFade ("Shell Fade With Distance (m)", Float) = 500

        [Header(Holes)]
        _HoleSink ("Drawn Sink (m the rim rolls down)", Float) = 60
        _LipHeight ("Lip Height (m)", Float) = 6
        _VoidColour ("Void", Color) = (0.17, 0.15, 0.27, 1)
        _VoidDarken ("Void Darken (the wall)", Range(0, 1)) = 0.85
        _HoleClear ("Hole Clear (cut where it starts to dissolve)", Range(0, 1)) = 0.55

        [Header(Edge)]
        _EdgeFade ("Edge Fade (fraction of the drawn distance)", Range(0.01, 0.5)) = 0.2
        _FogDepth ("Fog Depth (m of cloud in front of ground until it is solid)", Float) = 12

        // A/B (temporary, 2026-11-24): how the soft tops are drawn. Layers: today's shells. Metre Edges: the shells, each
        // edge fading over Edge Metres of real distance. Fog Pass: no shells; the surface adds the fog a view ray crosses
        // above it, up to Fog Height. Off: the drawn sea is not drawn at all, to judge the mist near the camera alone
        // (plan cloud-sea-fog.md, F4). The loser and this switch go once one is picked.
        [Header(AB Sea Look)]
        [Enum(Layers, 0, Metre Edges, 1, Fog Pass, 2, Off, 3)] _SeaLook ("Sea Look", Float) = 0
        _EdgeMetres ("Edge Metres (Metre Edges)", Range(0.1, 30)) = 4
        _FogHeight ("Fog Height (m, Fog Pass)", Range(1, 60)) = 18
        _FogWind ("Fog Wind (m/s on the ground, Fog Pass)", Vector) = (2.5, 0.8, 0, 0)
        _FogCoverage ("Fog Coverage (0 all fog .. clear gaps between puffs, Fog Pass)", Range(0, 0.9)) = 0.35
        _WispStrength ("Wisp Strength (the streaks on the wind, Fog Pass)", Range(0, 3)) = 1
        _SeeThrough ("See Through (the gaps open onto the deep, Fog Pass)", Range(0, 1)) = 0.5
        _DeepColour ("Deep (seen through the gaps, Fog Pass)", Color) = (0.62, 0.7, 0.85, 1)
        _FogShape ("Fog Shape (how strongly the puffs are lit as bumps, Fog Pass)", Range(0, 1)) = 0.3

        // The undercut: a hole's or rift's wall tucks back under the cloud as it goes down (visual only).
        [Header(Rift Walls)]
        _Curl ("Curl (m the lower wall tucks back under the edge)", Range(0, 80)) = 25

        // Set by CloudSeaPresenter: which layer this draw is (0 the surface), how many, how far apart, and the shells'
        // own grid half-width; the shells draw without depth writes.
        [HideInInspector] _ShellLayer ("Shell Layer", Float) = 0
        [HideInInspector] _ShellCount ("Shell Count", Float) = 0
        [HideInInspector] _ShellSpacing ("Shell Spacing", Float) = 1.5
        [HideInInspector] _ShellHalf ("Shell Grid Half", Float) = 600
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off // the underside shows only from below (Frag)

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The sun's shadows on the fog (Fog Pass): the ship's and the islands' (main light shadows) and the sky
            // clouds' (the vendored volumetric clouds hand theirs over as the main light's cookie).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Include/TinCanLighting.hlsl"
            #include "Include/TinCanAtmosphere.hlsl"
            #include "Include/TinCanCloudSea.hlsl"
            #include "Include/TinCanCloudLook.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LitColour;
                half4 _ShadowColour;
                half4 _HollowColour;
                half _HollowDarken;
                half _SunTint;
                half _Ambient;
                half _Wrap;
                half _BandSoftness;
                half _Bands;
                half _Rim;
                half _RimPower;
                half _SilverLining;
                half _SilverFocus;
                half _Translucency;
                float _LumpScale;
                float _LumpHeight;
                float _LumpScale2;
                float _LumpHeight2;
                float _LumpMorph;
                float _BillowScale;
                half _BillowStrength;
                float _FluffScale;
                half _FluffSoftness;
                float _FluffMorph;
                half _ShellOpacity;
                float _ShellFade;
                float _HoleSink;
                float _LipHeight;
                half4 _VoidColour;
                half _VoidDarken;
                half _HoleClear;
                half _EdgeFade;
                float _FogDepth;
                float _SeaLook;
                float _EdgeMetres;
                float _FogHeight;
                float4 _FogWind;
                half _FogCoverage;
                half _WispStrength;
                half _SeeThrough;
                half4 _DeepColour;
                half _FogShape;
                float _Curl;
                float _ShellLayer;
                float _ShellCount;
                float _ShellSpacing;
                float _ShellHalf;
                float _ZWrite;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 sea : TEXCOORD2;    // x heap 0 hollow .. 1 top, y hole cut, z swell 0 trough .. 1 crest
                float2 centre : TEXCOORD3; // the grid's centre (the camera, snapped)
            };

            #define ValueNoise TinCanCloudNoise

            // Fog over ground: how solid this layer is, from the metres of cloud the view ray crosses between it and
            // the opaque scene behind it (the depth texture holds opaques only, never the sea). A layer just above an
            // island's slope fades out, so the layers sink into the ground as a continuous fog instead of each cutting
            // it in a contour line; open sea and sky lie far behind and are untouched. 1 when there is no depth
            // behind this point (no depth texture, or the scene is in front of it).
            half FogOverGround(float4 positionCS, float3 positionWS)
            {
                float2 uv = positionCS.xy / _ScaledScreenParams.xy;
                float raw = SampleSceneDepth(uv);
                // Sky (nothing drawn) sits at the far plane: no ground behind, or the far sea would fade into the sky.
                #if UNITY_REVERSED_Z
                    if (raw <= 1e-6) return 1.0h;
                #else
                    if (raw >= 1.0 - 1e-6) return 1.0h;
                #endif
                float sceneEye = LinearEyeDepth(raw, _ZBufferParams);
                float fragmentEye = LinearEyeDepth(positionWS, UNITY_MATRIX_V);
                float behind = sceneEye - fragmentEye;
                if (behind < -0.01) return 1.0h;
                float rayMetres = behind * distance(positionWS, GetCameraPositionWS()) / max(fragmentEye, 1e-3);
                return (half)(1.0 - exp(-3.0 * rayMetres / max(_FogDepth, 0.01)));
            }

            // Billow noise (TinCanCloudLook.hlsl) on the sea, slowly morphing with the shared time.
            float Billow(float2 xz, float scale, float morph)
            {
                return TinCanCloudBillow(float3(xz / max(scale, 0.01), _TinCanCloudSea.x * morph));
            }

            // The heaps, 0..1: big ones with small ones on top.
            float Heaps(float2 xz)
            {
                float big = Billow(xz, _LumpScale, _LumpMorph);
                float small = Billow(xz + 71.3, _LumpScale2, _LumpMorph * 1.7);
                return big * 0.7 + small * big * 0.3 + small * 0.1;
            }

            // The drawn surface: the game's swell and cut (sinking only as far as the drawn sink), the heaps (flattened
            // inside a hole), and the lip.
            float DrawnHeight(float2 xz, out float cut, out float heaps)
            {
                cut = TinCanCloudHoleCut(xz); // the server's holes and the web of rifts
                heaps = Heaps(xz);
                float lumps = (heaps * _LumpHeight * 0.85 + Billow(xz + 71.3, _LumpScale2, _LumpMorph * 1.7) * _LumpHeight2) * (1.0 - cut);
                return _TinCanCloudSea.y + TinCanCloudSwell(xz) + lumps - cut * _HoleSink + TinCanCloudHoleLip(xz) * _LipHeight;
            }

            // The drifting fluff the shells are cut from, 0..1.
            float Fluff(float2 xz)
            {
                float3 p = float3(xz / max(_FluffScale, 0.01), _TinCanCloudSea.x * _FluffMorph);
                return ValueNoise(p) * 0.65 + ValueNoise(p * 2.3 + 17.1) * 0.35;
            }

            // How high the soft tops reach at a point, 0..1 of the stack: fluff, densest over the heaps.
            float TopDensity(float2 xz)
            {
                return saturate(Fluff(xz) * 0.65 + Heaps(xz) * 0.55);
            }

            // Fog Pass: the fog's density at a point, its fluff carried off by the wind while the heaps under it stay.
            float FogDensity(float2 xz, float2 drift)
            {
                float density = saturate(Fluff(xz - drift) * 0.65 + Heaps(xz) * 0.55);
                // Coverage clears the thinnest fog, so there is air between the puffs instead of an even haze.
                return smoothstep(_FogCoverage, 1.0, density);
            }

            // How much sun reaches a point of the fog, 0..1: the ship's and the islands' shadows, and the sky clouds'.
            half SunOnFog(float3 positionWS)
            {
                half sun = MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS));
                #if defined(_LIGHT_COOKIES)
                    // The sky clouds' shadows at half strength: light scatters through fog, and at full strength they
                    // lay dark smears across the sea.
                    sun *= lerp(1.0h, (half)Luminance(SampleMainLightCookie(positionWS)), 0.5h);
                #endif
                return sun;
            }

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(positionOS.xyz);
                float cut, heaps, ignoreHeaps;
                float height = DrawnHeight(world.xz, cut, heaps);

                // The normal from the drawn height around this point, so the light follows the swell and the heaps.
                const float probe = 3.0;
                float cutXp, cutXm, cutZp, cutZm;
                float dx = DrawnHeight(world.xz + float2(probe, 0), cutXp, ignoreHeaps) - DrawnHeight(world.xz - float2(probe, 0), cutXm, ignoreHeaps);
                float dz = DrawnHeight(world.xz + float2(0, probe), cutZp, ignoreHeaps) - DrawnHeight(world.xz - float2(0, probe), cutZm, ignoreHeaps);
                float3 normal = normalize(float3(-dx, 2.0 * probe, -dz));

                // The undercut (visual only): down a hole's or rift's wall the cloud tucks back under its own edge, away
                // from the opening, so the lower wall curls in under the top instead of standing like a cliff. Done by
                // the visible part of the wall (the rest has dissolved, _HoleClear).
                float2 intoHole = float2(cutXp - cutXm, cutZp - cutZm);
                float slope = length(intoHole);
                if (slope > 1e-5)
                {
                    float2 away = -intoHole / slope;
                    float under = smoothstep(0.2, 0.75, cut);
                    world.xz += away * (_Curl * under);
                    normal = normalize(lerp(normal, normalize(float3(-away.x, -0.6, -away.y)), smoothstep(0.35, 0.75, cut)));
                }
                output.normalWS = normal;

                world.y = height + _ShellLayer * _ShellSpacing;
                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                float amplitude = max(_TinCanCloudSea.z, 1e-3);
                float swell = _TinCanCloudSea.z > 0.0 ? saturate(TinCanCloudSwell(world.xz) / amplitude * 0.5 + 0.5) : 0.5;
                output.sea = float3(heaps, cut, swell);
                output.centre = TransformObjectToWorld(float3(0, 0, 0)).xz;
                return output;
            }

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                if (_SeaLook > 2.5) discard; // Off: the mist alone (A/B)

                // The underside (plan cloud-sea.md, step 3) only where the camera is below this point of the sea, so
                // diving under shows a cloud ceiling, and from above nothing changes (no back of a hole's wall shows).
                bool underside = !front;
                if (underside && GetCameraPositionWS().y > input.positionWS.y) discard;

                float2 xz = input.positionWS.xz;
                float heaps = input.sea.x;
                float cut = input.sea.y;
                bool shell = _ShellLayer > 0.5;
                if (shell && _SeaLook > 1.5) discard; // Fog Pass: the surface draws the tops itself
                float layer = shell ? saturate(_ShellLayer / max(_ShellCount, 1.0)) : 0.0;

                // Fine billows tip the normal in the light; the shells reuse the fluff instead.
                float3 normal = normalize(input.normalWS);
                float fluff = 0.0;
                if (shell)
                {
                    fluff = Fluff(xz);
                }
                else
                {
                    float3 p = float3(xz / max(_BillowScale, 0.01), _TinCanCloudSea.x * _LumpMorph * 3.0);
                    float b = ValueNoise(p);
                    float bx = ValueNoise(p + float3(0.2, 0, 0)) - b;
                    float bz = ValueNoise(p + float3(0, 0.2, 0)) - b;
                    normal = normalize(normal + float3(-bx, 0, -bz) * _BillowStrength * 4.0);
                }

                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Hollows between heaps and the swell's troughs lean cool; higher shells are the thin, sunlit tops.
                half hollow = (half)saturate((1.0 - heaps) * 0.75 + (1.0 - input.sea.z) * 0.35) * (1.0h - (half)layer);
                // Seen from below, the sea is the shadowed underside of a cloud: facing down, all hollow.
                if (underside)
                {
                    normal = -normal;
                    hollow = 1.0h;
                }
                TINCAN_CLOUD_LOOK(look)
                half3 colour = TinCanCloudShade(look, normal, view, hollow, (half)layer);
                colour = TinCanWeatherShade(colour, input.positionWS);

                // A hole's wall darkens toward the void as it rolls down.
                colour = lerp(colour, _VoidColour.rgb, _VoidDarken * smoothstep(0.0, 1.0, cut));
                colour = TinCanNightShade(colour);

                // Fog Pass: the fog standing on the surface up to Fog Height times the top density; a view ray crosses
                // more of it the flatter it looks, and the fog takes Fog Depth metres to turn solid.
                half seaAlpha = 1.0h;
                if (!shell && !underside && _SeaLook > 1.5)
                {
                    float time = _TinCanCloudSea.x;
                    float2 drift = _FogWind.xy * time;
                    half top = (half)(FogDensity(xz, drift) * (1.0 - cut));

                    // Into it: past where the view ray meets the sea it goes on down; the fog deeper along it slides
                    // against the top as the camera moves, and drifts slower the deeper it is.
                    float2 dive = -view.xz / max(view.y, 0.15);
                    half inner = 0.0h;
                    [unroll] for (int k = 1; k <= 3; k++)
                    {
                        inner += (half)FogDensity(xz + dive * (_FogHeight * 0.3 * k), drift * (1.0 - 0.2 * k));
                    }
                    inner = inner / 3.0h * (half)(1.0 - cut);

                    // Fine wisps streaking along the wind, faster than the body.
                    float speed = length(_FogWind.xy);
                    float2 windDir = speed > 1e-4 ? _FogWind.xy / speed : float2(1, 0);
                    float2 along = float2(dot(xz, windDir), dot(xz, float2(-windDir.y, windDir.x))) - float2(speed * time * 2.5, 0);
                    half wisp = (half)ValueNoise(float3(along / float2(max(_FluffScale, 0.01) * 0.9, max(_FluffScale, 0.01) * 0.25), time * _FluffMorph * 2.0)) - 0.5h;

                    // The wisps reach into the gaps too, as thin veils, strongest over the puffs.
                    half body = saturate(top * 0.6h + inner * 0.4h + wisp * _WispStrength * (0.15h + 0.35h * top));
                    float path = body * _FogHeight / max(view.y, 0.15);
                    half fog = (half)(1.0 - exp(-3.0 * path / max(_FogDepth, 0.01)));

                    // Its shape: the fog's top is a height field (density times Fog Height), so each puff has a sunlit
                    // side and a shaded side, like the billows of a real cloud top.
                    const float probe = 2.0;
                    float topX = FogDensity(xz + float2(probe, 0), drift);
                    float topZ = FogDensity(xz + float2(0, probe), drift);
                    float shape = _FogHeight * _FogShape;
                    float3 fogNormal = normalize(float3(-(topX - top) * shape, probe, -(topZ - top) * shape));

                    // Light through it: thin fog is bright and lets the sun through, thick cores deep inside darken.
                    half thin = saturate(1.0h - inner * 1.2h + wisp * 0.3h);
                    half core = saturate(inner - top * 0.5h) * 0.8h;
                    half3 fogColour = TinCanCloudShade(look, fogNormal, view, core, thin);

                    // Shadows: the sun blocked by the ship, the islands or the clouds above.
                    half sun = SunOnFog(input.positionWS);
                    half3 fogInShadow = TinCanCloudShadeLit(look, 0.0h, SampleSH(fogNormal), 0.0h, view, core, thin * 0.3h);
                    fogColour = lerp(fogInShadow, fogColour, sun);
                    colour *= lerp(0.7h, 1.0h, sun);

                    fogColour = TinCanWeatherShade(fogColour, input.positionWS);
                    fogColour = TinCanNightShade(fogColour);

                    // See through: under the fog the surface is not ground but the deep, so where the fog is thin it
                    // darkens toward the deep and lets what lies below show a little. The fog itself stays as solid
                    // as it is: over a puff nothing changes, in a gap you look down into air.
                    // Through a gap: the lower fog (the deeper samples, sliding with parallax) in shade, and where
                    // there is none of it, the hazy deep.
                    half3 lower = TinCanCloudShade(look, float3(0, 1, 0), view, 0.7h, 0.2h);
                    half3 deep = lerp(_DeepColour.rgb, lower, saturate(inner * 1.5h));
                    deep = TinCanNightShade(TinCanWeatherShade(deep, input.positionWS));
                    colour = lerp(colour, deep, _SeeThrough);
                    half surfaceAlpha = 1.0h - _SeeThrough * 0.7h;
                    seaAlpha = fog + (1.0h - fog) * surfaceAlpha;
                    colour = (fogColour * fog + colour * (1.0h - fog) * surfaceAlpha) / max(seaAlpha, 1e-3h);
                }

                half alpha = seaAlpha * (1.0h - (half)smoothstep(_HoleClear, 1.0, cut));
                // The surface fades by real distance (inside the camera's far plane); the shells by their own square grid.
                float reach = shell ? max(abs(xz.x - input.centre.x), abs(xz.y - input.centre.y)) : distance(input.positionWS, GetCameraPositionWS());
                float half_extent = shell ? _ShellHalf : _TinCanCloudSwell.w;
                float edge = half_extent > 0.0 ? reach / half_extent : 0.0;
                alpha *= 1.0h - (half)smoothstep(1.0 - _EdgeFade, 1.0, edge);
                // From above, thin where ground lies just behind; from below, what is behind is air, not cloud.
                if (!underside) alpha *= FogOverGround(input.positionCS, input.positionWS);

                if (shell)
                {
                    // One fluff field cut at a rising threshold: each layer keeps less, so the stack rounds into soft
                    // heaps, densest above the surface's own heaps.
                    float density = saturate(fluff * 0.65 + heaps * 0.55) * (1.0 - cut);
                    half coverage = (half)smoothstep(layer, layer + _FluffSoftness, density);
                    if (_SeaLook > 0.5)
                    {
                        // Metre Edges: how many metres inside this layer's edge the point is (the density past the
                        // cut over how fast it changes per metre), faded over Edge Metres.
                        const float probe = 1.0;
                        float here = TopDensity(xz);
                        float2 slope = float2(TopDensity(xz + float2(probe, 0)) - here, TopDensity(xz + float2(0, probe)) - here) / probe;
                        float inside = (here * (1.0 - cut) - layer) / max(length(slope), 1e-4);
                        coverage = (half)smoothstep(0.0, _EdgeMetres, inside);
                    }
                    alpha *= coverage * _ShellOpacity;
                    alpha *= 1.0h - (half)smoothstep(_ShellFade * 0.5, _ShellFade, distance(input.positionWS, GetCameraPositionWS()));
                    clip(alpha - 0.002h);
                }

                return TinCanAtmospherePremultiplied(half4(colour * alpha, alpha), input.positionWS);
            }
            ENDHLSL
        }
    }
}
