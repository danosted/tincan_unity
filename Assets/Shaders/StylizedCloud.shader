// Stylized clouds (.docs/ART_DIRECTION.md, "Sky and clouds"; plan .docs/plans/look-slice.md, "The cloud lab"): a cloud is
// one mesh of overlapping puffs (DevTools/Editor/CloudShapes.cs), shaded as one soft mass rather than a pile of balls.
// - Lighting reads a normal blended from each puff's own normal toward the whole cloud's shape normal (TEXCOORD1), in
//   the world's bands (TinCanBand): cream lit tops, lilac shadow sides, a darker base (TEXCOORD0.x: height in the cloud).
//   With a wide band softness and wrap the bands melt into one soft gradient (the M_CloudSoft materials).
// - Creases: darker where puffs meet or the surface folds in (TEXCOORD0.y, baked by CloudShapes), for depth between
//   lobes; and large, faint mottling, so a lobe is not one flat colour. Both off at 0.
// - A bright rim toward the sun, and a silver lining along the edges when looking into the light.
// - A depth pre-pass, so only the cloud's outer silhouette fades (by the shape normal's facing) and the puffs inside
//   never sort against each other.
// - The air (TinCanAtmosphere.hlsl) fades it like everything else.
Shader "TinCan/Environment/Stylized Cloud"
{
    Properties
    {
        [Header(Colour)]
        _LitColour ("Lit", Color) = (0.984, 0.965, 0.925, 1)
        _ShadowColour ("Shadow", Color) = (0.663, 0.651, 0.784, 1)
        _BaseColour ("Base (underside)", Color) = (0.53, 0.5, 0.68, 1)
        _SunTint ("Sun Tint (lit side takes the sun's colour)", Range(0, 1)) = 0.6
        _Ambient ("Ambient (sky fill in shadow)", Range(0, 1)) = 0.25

        [Header(Shape and bands)]
        _ShapeBlend ("Shape Normals (0 puffs, 1 one smooth mass)", Range(0, 1)) = 0.65
        _Wrap ("Wrap (lit band reaches past the terminator)", Range(0, 1)) = 0.35
        _BandSoftness ("Band Softness", Range(0.01, 1)) = 0.2
        _Bands ("Bands (2 or 3)", Range(2, 3)) = 2
        _BaseDarken ("Base Darken", Range(0, 1)) = 0.55
        _BaseHeight ("Base Height (fraction of the cloud)", Range(0.01, 1)) = 0.3

        [Header(Depth inside)]
        _Crease ("Crease (darkens where puffs meet)", Range(0, 1)) = 0
        _CreaseColour ("Crease Colour", Color) = (0.6, 0.58, 0.6, 1)
        _Mottle ("Mottle (large faint blotches)", Range(0, 0.5)) = 0
        _MottleScale ("Mottle Size (m)", Float) = 40

        [Header(Light at the edges)]
        _Rim ("Rim", Range(0, 2)) = 0.5
        _RimWidth ("Rim Width", Range(0.01, 1)) = 0.3
        _SilverLining ("Silver Lining (looking into the sun)", Range(0, 4)) = 1.5
        _SilverFocus ("Silver Lining Focus", Range(1, 32)) = 6

        [Header(Silhouette)]
        _EdgeShape ("Edge From Shape (0 puff outlines, 1 the cloud's)", Range(0, 1)) = 0.85
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.3
        _BottomFade ("Bottom Fade (fades out over this much of the height; 0 off)", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _LitColour;
            half4 _ShadowColour;
            half4 _BaseColour;
            half _SunTint;
            half _Ambient;
            half _ShapeBlend;
            half _Wrap;
            half _BandSoftness;
            half _Bands;
            half _BaseDarken;
            half _BaseHeight;
            half _Crease;
            half4 _CreaseColour;
            half _Mottle;
            float _MottleScale;
            half _Rim;
            half _RimWidth;
            half _SilverLining;
            half _SilverFocus;
            half _EdgeShape;
            half _EdgeSoftness;
            half _BottomFade;
            half _Opacity;
        CBUFFER_END
        ENDHLSL

        // Drawn before the forward pass (URP draws SRPDefaultUnlit first): the nearest puff's depth, so the colour pass
        // keeps only the front surface of the cloud.
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/TinCanLighting.hlsl"
            #include "Include/TinCanAtmosphere.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;     // the puff's own normal
                float2 cloud : TEXCOORD0;     // x: height in the cloud, 0 base .. 1 top; y: crease, 0 open .. 1 deep in a fold
                float3 shapeOS : TEXCOORD1;   // the whole cloud's shape normal
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 puffWS : TEXCOORD1;
                float3 shapeWS : TEXCOORD2;
                float2 cloud : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
            };

            // Value noise, two octaves: large, soft blotches.
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

            half Mottle(float3 p)
            {
                return (half)(ValueNoise(p) * 0.65 + ValueNoise(p * 2.3 + 17.1) * 0.35);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.puffWS = TransformObjectToWorldNormal(input.normalOS);
                output.shapeWS = TransformObjectToWorldNormal(input.shapeOS);
                output.cloud = input.cloud;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 puff = normalize(input.puffWS);
                float3 shape = normalize(input.shapeWS);
                float3 normal = normalize(lerp(puff, shape, _ShapeBlend));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight();

                half lit = TinCanBand(dot(normal, sun.direction), _Wrap, _BandSoftness, _Bands);
                half3 lightColour = lerp(1.0h, sun.color, _SunTint);
                half3 colour = lerp(_ShadowColour.rgb, _LitColour.rgb * lightColour, lit);
                colour += SampleSH(normal) * _Ambient * (1.0h - lit);
                colour = lerp(colour, _BaseColour.rgb, _BaseDarken * (1.0h - smoothstep(0.0h, _BaseHeight, input.cloud.x)));
                colour = lerp(colour, _CreaseColour.rgb * lightColour, _Crease * input.cloud.y);
                if (_Mottle > 0.0h) colour *= 1.0h + (Mottle(input.positionOS / max(_MottleScale, 0.01)) - 0.5h) * 2.0h * _Mottle;

                // Rim: a thin bright band on the outline; on the lit side it takes the sun, in shadow the sky.
                half edge = 1.0h - saturate(dot(normal, view));
                half rim = smoothstep(1.0h - _RimWidth, 1.0h, edge) * _Rim;
                colour += lerp(SampleSH(normal) * 0.5h, sun.color, lit) * rim;
                // Silver lining: light scattered forward through the thin edges, seen when looking toward the sun.
                half toward = pow(saturate(dot(-view, sun.direction)), _SilverFocus);
                colour += sun.color * toward * smoothstep(0.4h, 1.0h, edge) * _SilverLining;

                half facing = saturate(dot(normalize(lerp(puff, shape, _EdgeShape)), view));
                half alpha = smoothstep(0.0h, _EdgeSoftness, facing) * _Opacity;
                // A sea surface dissolves where it dips toward its floor (hole rims) instead of ending in a cut edge.
                if (_BottomFade > 0.0h) alpha *= smoothstep(0.0h, _BottomFade, input.cloud.x);
                return TinCanAtmospherePremultiplied(half4(colour * alpha, alpha), input.positionWS);
            }
            ENDHLSL
        }
    }
}
