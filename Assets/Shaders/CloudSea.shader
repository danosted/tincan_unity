Shader "TinCan/Environment/Cloud Sea"
{
    Properties
    {
        _ShallowColor ("Cloud Highlight", Color) = (0.96, 0.98, 1, 0.88)
        _DeepColor ("Cloud Shadow", Color) = (0.55, 0.66, 0.78, 0.72)
        _CloudHeight ("Cloud Height", Range(0, 30)) = 14
        _HeightNoiseScale ("Height Noise Scale", Float) = 0.006
        _NoiseScale ("Noise Scale", Float) = 0.012
        _NoiseStrength ("Density Contrast", Range(0, 1)) = 0.16
        _ScrollSpeed ("Scroll Speed", Vector) = (0.35, 0.16, 0, 0)
        _EdgeSoftness ("Horizon Softness", Range(0.1, 0.8)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float3 viewDirectionWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _CloudHeight;
                float _HeightNoiseScale;
                float _NoiseScale;
                float _NoiseStrength;
                float4 _ScrollSpeed;
                float _EdgeSoftness;
            CBUFFER_END

            float Hash21(float2 samplePosition)
            {
                samplePosition = frac(samplePosition * float2(123.34, 456.21));
                samplePosition += dot(samplePosition, samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            float ValueNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 fraction = frac(samplePosition);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(
                    lerp(Hash21(cell), Hash21(cell + float2(1, 0)), fraction.x),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1), fraction.x),
                    fraction.y);
            }

            float FractalNoise(float2 samplePosition)
            {
                float noise = ValueNoise(samplePosition) * 0.58;
                noise += ValueNoise(samplePosition * 2.03 + 17.4) * 0.28;
                noise += ValueNoise(samplePosition * 4.11 - 8.7) * 0.14;
                return noise;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 heightScroll = _Time.y * _ScrollSpeed.xy * 0.08;
                float heightNoise = FractalNoise(positionWS.xz * _HeightNoiseScale + heightScroll);
                positionWS.y += smoothstep(0.18, 0.88, heightNoise) * _CloudHeight;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 scroll = _Time.y * _ScrollSpeed.xy;
                float broad = FractalNoise(input.positionWS.xz * _NoiseScale + scroll);
                float detail = ValueNoise(input.positionWS.xz * _NoiseScale * 3.7 - scroll * 0.6);
                float density = saturate(broad * 0.78 + detail * 0.22);
                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                normalWS *= normalWS.y < 0 ? -1 : 1;
                float diffuse = saturate(dot(normalWS, normalize(float3(-0.35, 0.8, -0.2))) * 0.5 + 0.5);
                half4 color = lerp(_DeepColor, _ShallowColor, saturate(density * 0.65 + diffuse * 0.35));
                color.rgb += (density - 0.5) * _NoiseStrength;
                color.rgb = lerp(color.rgb, _ShallowColor.rgb, saturate(input.fogFactor * 0.45));
                float facing = abs(dot(normalWS, normalize(input.viewDirectionWS)));
                float noisyFacing = facing + (density - 0.5) * 0.16;
                float edgeFade = smoothstep(0.02, _EdgeSoftness, noisyFacing);
                color.a *= edgeFade * lerp(0.72, 1.0, density);
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
