// The drawn cloud sea (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"; ART_DIRECTION.md, "Sky and
// clouds"): a flat grid around the camera (CloudSeaPresenter) lifted by the shared swell and sunk into the server's
// holes, with the formula the game reads (Include/TinCanCloudSea.hlsl). On top of that, visual only:
// - Lumps: billow noise heaps the surface into rounded mounds with creases between them, at two sizes, slowly
//   morphing, so the crests break into cauliflower edges instead of reading as smooth hills.
// - Shells: the same grid drawn again in thin see-through layers above the surface (_ShellLayer 1..N, set by the
//   presenter), each cut by one drifting fluff field at a rising threshold, so the tops are soft and fray, light
//   passes through thin edges, and you see a little way into the cloud. Layer 0 is the surface itself.
// Shaded like the soft clouds, not like water: cream lit heaps, cool hollows, a sky-coloured rim at grazing angles,
// light through the thin tops toward the sun. A hole's rim rolls down into it, darkens toward the void and dissolves;
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
        _EdgeFade ("Edge Fade (fraction of the grid)", Range(0.01, 0.5)) = 0.2

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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Include/TinCanLighting.hlsl"
            #include "Include/TinCanAtmosphere.hlsl"
            #include "Include/TinCanCloudSea.hlsl"

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

            float Hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), f.x), lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), f.x), lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + 1), f.x), f.y),
                    f.z);
            }

            // Billow noise: rounded heaps with sharp creases between them, 0 in a crease .. 1 on a heap.
            float Billow(float2 xz, float scale, float morph)
            {
                float n = ValueNoise(float3(xz / max(scale, 0.01), _TinCanCloudSea.x * morph));
                return 1.0 - abs(n * 2.0 - 1.0);
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
                cut = TinCanCloudHoleCut(xz);
                heaps = Heaps(xz);
                float lumps = (heaps * _LumpHeight * 0.85 + Billow(xz + 71.3, _LumpScale2, _LumpMorph * 1.7) * _LumpHeight2) * (1.0 - cut);
                return _TinCanCloudSea.y + TinCanCloudSwell(xz) + lumps - cut * _HoleSink + TinCanCloudHoleLip(xz) * _LipHeight;
            }

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(positionOS.xyz);
                float cut, heaps, ignoreCut, ignoreHeaps;
                float height = DrawnHeight(world.xz, cut, heaps);

                // The normal from the drawn height around this point, so the light follows the swell and the heaps.
                const float probe = 3.0;
                float dx = DrawnHeight(world.xz + float2(probe, 0), ignoreCut, ignoreHeaps) - DrawnHeight(world.xz - float2(probe, 0), ignoreCut, ignoreHeaps);
                float dz = DrawnHeight(world.xz + float2(0, probe), ignoreCut, ignoreHeaps) - DrawnHeight(world.xz - float2(0, probe), ignoreCut, ignoreHeaps);
                output.normalWS = normalize(float3(-dx, 2.0 * probe, -dz));

                world.y = height + _ShellLayer * _ShellSpacing;
                output.positionWS = world;
                output.positionCS = TransformWorldToHClip(world);
                float amplitude = max(_TinCanCloudSea.z, 1e-3);
                float swell = _TinCanCloudSea.z > 0.0 ? saturate(TinCanCloudSwell(world.xz) / amplitude * 0.5 + 0.5) : 0.5;
                output.sea = float3(heaps, cut, swell);
                output.centre = TransformObjectToWorld(float3(0, 0, 0)).xz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;
                float heaps = input.sea.x;
                float cut = input.sea.y;
                bool shell = _ShellLayer > 0.5;
                float layer = shell ? saturate(_ShellLayer / max(_ShellCount, 1.0)) : 0.0;

                // Fine billows tip the normal in the light; the shells reuse the fluff instead.
                float3 normal = normalize(input.normalWS);
                float fluff = 0.0;
                if (shell)
                {
                    float3 p = float3(xz / max(_FluffScale, 0.01), _TinCanCloudSea.x * _FluffMorph);
                    fluff = ValueNoise(p) * 0.65 + ValueNoise(p * 2.3 + 17.1) * 0.35;
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
                Light sun = GetMainLight();
                half lit = TinCanBand((half)dot(normal, sun.direction), _Wrap, _BandSoftness, _Bands);
                // Higher shells are the cloud's thin, sunlit tops.
                lit = saturate(lit + (half)layer * 0.35h);
                half3 lightColour = lerp(1.0h, sun.color, _SunTint);
                half3 colour = lerp(_ShadowColour.rgb, _LitColour.rgb * lightColour, lit);
                colour += SampleSH(normal) * _Ambient * (1.0h - lit);

                // Hollows between heaps and the swell's troughs lean cool.
                half hollow = (half)saturate((1.0 - heaps) * 0.75 + (1.0 - input.sea.z) * 0.35) * (1.0h - (half)layer);
                colour = lerp(colour, _HollowColour.rgb * lightColour, _HollowDarken * hollow);

                // Grazing angles take the sky; toward the sun, light through the thin tops and a silver lining.
                half grazing = (half)pow(1.0 - saturate(dot(normal, view)), _RimPower);
                colour = lerp(colour, SampleSH(float3(0, 1, 0)) + sun.color * 0.25h, grazing * _Rim);
                half toward = (half)pow(saturate(dot(-view, sun.direction)), _SilverFocus);
                colour += sun.color * toward * (grazing * _SilverLining + (half)layer * _Translucency);

                // A hole's wall darkens toward the void as it rolls down.
                colour = lerp(colour, _VoidColour.rgb, _VoidDarken * smoothstep(0.0, 1.0, cut));
                colour = TinCanNightShade(colour);

                half alpha = 1.0h - (half)smoothstep(_HoleClear, 1.0, cut);
                float reach = max(abs(xz.x - input.centre.x), abs(xz.y - input.centre.y));
                float half_extent = shell ? _ShellHalf : _TinCanCloudSwell.w;
                float edge = half_extent > 0.0 ? reach / half_extent : 0.0;
                alpha *= 1.0h - (half)smoothstep(1.0 - _EdgeFade, 1.0, edge);

                if (shell)
                {
                    // One fluff field cut at a rising threshold: each layer keeps less, so the stack rounds into soft
                    // heaps, densest above the surface's own heaps.
                    float density = saturate(fluff * 0.65 + heaps * 0.55) * (1.0 - cut);
                    alpha *= (half)smoothstep(layer, layer + _FluffSoftness, density) * _ShellOpacity;
                    alpha *= 1.0h - (half)smoothstep(_ShellFade * 0.5, _ShellFade, distance(input.positionWS, GetCameraPositionWS()));
                    clip(alpha - 0.002h);
                }

                return TinCanAtmospherePremultiplied(half4(colour * alpha, alpha), input.positionWS);
            }
            ENDHLSL
        }
    }
}
