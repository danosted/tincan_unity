Shader "TinCan/Environment/Cloud Puff"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.92, 0.95, 1, 0.72)
        _ShadowColor ("Shadow Color", Color) = (0.4, 0.52, 0.68, 0.65)
        _EdgeSoftness ("Edge Softness", Range(0.05, 0.8)) = 0.42
        _FlowScale ("Flow Scale", Float) = 0.035
        _FlowSpeed ("Flow Speed", Float) = 0.12
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.22
        _ShapeScale ("Shape Noise Scale", Float) = 9
        _ShapeStrength ("Shape Carve Strength", Range(0, 1)) = 0.6
        _DetailScale ("Detail Noise Scale", Float) = 26
        _DetailStrength ("Detail Carve Strength", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirectionWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float3 instanceSeed : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                float _EdgeSoftness;
                float _FlowScale;
                float _FlowSpeed;
                float _FlowStrength;
                float _ShapeScale;
                float _ShapeStrength;
                float _DetailScale;
                float _DetailStrength;
            CBUFFER_END

            float Hash31(float3 samplePosition)
            {
                samplePosition = frac(samplePosition * 0.1031);
                samplePosition += dot(samplePosition, samplePosition.yzx + 33.33);
                return frac((samplePosition.x + samplePosition.y) * samplePosition.z);
            }

            float ValueNoise(float3 samplePosition)
            {
                float3 cell = floor(samplePosition);
                float3 fraction = frac(samplePosition);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                return lerp(
                    lerp(
                        lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), fraction.x),
                        lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), fraction.x),
                        fraction.y),
                    lerp(
                        lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), fraction.x),
                        lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + 1), fraction.x),
                        fraction.y),
                    fraction.z);
            }

            float FBM3(float3 samplePosition)
            {
                float value = ValueNoise(samplePosition) * 0.55;
                value += ValueNoise(samplePosition * 2.11 + 11.3) * 0.29;
                value += ValueNoise(samplePosition * 4.37 - 5.9) * 0.16;
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                // Per-instance world offset seeds the shape noise so every puff carves a unique silhouette
                // from the same shared unit-sphere mesh, instead of all puffs looking identically round.
                output.instanceSeed = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float upward = saturate(input.normalWS.y * 0.5 + 0.5);
                float facing = saturate(dot(normalize(input.normalWS), input.viewDirectionWS));

                // Carve the view-facing gradient (1 at the puff's center, 0 at its silhouette edge)
                // with fractal noise so the edge frays unevenly instead of fading in a perfect circle.
                // The sphere mesh is a hollow shell, so every fragment has the same distance-from-center;
                // "facing" is what actually varies from center to edge, so the noise must perturb that.
                float3 shapeSample = input.positionOS * _ShapeScale + input.instanceSeed * 0.07;
                float shapeNoise = FBM3(shapeSample);
                float3 detailSample = input.positionOS * _DetailScale + input.instanceSeed * 0.13;
                float detailNoise = FBM3(detailSample);
                float carvedFacing = facing
                    + (shapeNoise - 0.5) * _ShapeStrength
                    + (detailNoise - 0.5) * _DetailStrength;
                float edgeFade = smoothstep(0.0, _EdgeSoftness, carvedFacing);

                float3 flowPosition = input.positionWS * _FlowScale;
                flowPosition += float3(_Time.y * _FlowSpeed, 0, _Time.y * _FlowSpeed * 0.63);
                float flow = ValueNoise(flowPosition);
                float density = saturate(lerp(1.0 - _FlowStrength, 1.0, flow));

                half4 color = lerp(_ShadowColor, _BaseColor, upward);
                color.rgb += (flow - 0.5) * 0.08;
                color.rgb += (shapeNoise - 0.5) * 0.1;
                color.a *= edgeFade * density;
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}

