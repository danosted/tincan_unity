Shader "TinCan/Environment/Cloud Puff"
{
    Properties
    {
        _BaseColor ("Sunlit Color", Color) = (0.92, 0.95, 1, 1)
        _AmbientColor ("Unlit Color", Color) = (0.62, 0.72, 0.84, 1)
        _DensityMap ("Density Map", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _Tiling ("Tiling", Range(0.25, 4)) = 1.2
        _DetailTiling ("Detail Tiling", Range(1, 8)) = 3.1
            _NoiseScale ("World Noise Scale", Range(0.005, 0.1)) = 0.025
            _DetailScale ("World Detail Scale", Range(0.02, 0.3)) = 0.08
        _FlowSpeed ("Flow Speed", Range(0, 0.2)) = 0.025
        _Opacity ("Opacity", Range(0, 1)) = 0.35
        _OpacityVariation ("Opacity Variation", Range(0, 1)) = 0.35
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.65
        _Displacement ("Surface Displacement", Range(0, 0.2)) = 0.06
        _SunlightInfluence ("Sunlight Influence", Range(0, 2)) = 1
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            TEXTURE2D(_DensityMap);
            SAMPLER(sampler_DensityMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AmbientColor;
                float _Tiling;
                float _DetailTiling;
                    float _NoiseScale;
                    float _DetailScale;
                float _FlowSpeed;
                float _Opacity;
                float _OpacityVariation;
                float _NormalStrength;
                float _Displacement;
                float _SunlightInfluence;
            CBUFFER_END

            float2 GetFlowOffset()
            {
                return _Time.y * _FlowSpeed * float2(1.0, 0.57);
            }

            float3 GetTriplanarWeights(float3 normalWS)
            {
                float3 weights = pow(abs(normalWS), 4.0);
                return weights / max(weights.x + weights.y + weights.z, 0.0001);
            }

            float SampleTriplanarDensity(float3 positionWS, float3 weights, float scale, float2 offset)
            {
                float xProjection = SAMPLE_TEXTURE2D(
                    _DensityMap, sampler_DensityMap, positionWS.zy * scale + offset).r;
                float yProjection = SAMPLE_TEXTURE2D(
                    _DensityMap, sampler_DensityMap, positionWS.xz * scale + offset).r;
                float zProjection = SAMPLE_TEXTURE2D(
                    _DensityMap, sampler_DensityMap, positionWS.xy * scale + offset).r;
                return dot(float3(xProjection, yProjection, zProjection), weights);
            }

            float SampleTriplanarDensityLod(float3 positionWS, float3 weights, float scale, float2 offset)
            {
                float xProjection = SAMPLE_TEXTURE2D_LOD(
                    _DensityMap, sampler_DensityMap, positionWS.zy * scale + offset, 0).r;
                float yProjection = SAMPLE_TEXTURE2D_LOD(
                    _DensityMap, sampler_DensityMap, positionWS.xz * scale + offset, 0).r;
                float zProjection = SAMPLE_TEXTURE2D_LOD(
                    _DensityMap, sampler_DensityMap, positionWS.xy * scale + offset, 0).r;
                return dot(float3(xProjection, yProjection, zProjection), weights);
            }

            float SampleDensity(float3 positionWS, float3 normalWS)
            {
                float3 weights = GetTriplanarWeights(normalWS);
                float2 flowOffset = GetFlowOffset();
                float broad = SampleTriplanarDensity(positionWS, weights, _NoiseScale, flowOffset);
                float detail = SampleTriplanarDensity(
                    positionWS, weights, _DetailScale, -flowOffset * 1.7);
                return saturate(broad * 0.72 + detail * 0.28);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float2 flowOffset = GetFlowOffset();
                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                    float3 weights = GetTriplanarWeights(normalWS);
                    float broadNoise = SampleTriplanarDensityLod(
                        positionWS, weights, _NoiseScale, flowOffset);
                float displacement = (broadNoise - 0.5) * _Displacement;
                float3 displacedPositionOS = input.positionOS.xyz + input.normalOS * displacement;
                output.positionWS = TransformObjectToWorld(displacedPositionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                    output.normalWS = normalWS;
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 baseNormalWS = normalize(input.normalWS);
                float density = SampleDensity(input.positionWS, baseNormalWS);
                float alpha = _Opacity * lerp(1.0 - _OpacityVariation, 1.0, density);

                float2 flowOffset = GetFlowOffset();
                half3 broadNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(
                    _NormalMap,
                    sampler_NormalMap,
                    input.uv * _Tiling + flowOffset), _NormalStrength);
                half3 detailNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(
                    _NormalMap,
                    sampler_NormalMap,
                    input.uv * _DetailTiling - flowOffset * 1.7), _NormalStrength * 0.45);
                half3 normalTS = normalize(half3(
                    broadNormal.xy + detailNormal.xy,
                    broadNormal.z * detailNormal.z));
                float3 tangentWS = normalize(input.tangentWS.xyz);
                float3 bitangentWS = normalize(cross(baseNormalWS, tangentWS) * input.tangentWS.w);
                float3 normalWS = normalize(
                    tangentWS * normalTS.x + bitangentWS * normalTS.y + baseNormalWS * normalTS.z);
                Light mainLight = GetMainLight();
                float diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 lightTint = lerp(half3(1.0, 1.0, 1.0), mainLight.color, 0.2);
                half3 sunlitColor = _BaseColor.rgb * lightTint * lerp(0.65, 1.0, diffuse);
                half3 color = lerp(
                    _AmbientColor.rgb,
                    sunlitColor,
                    saturate(_SunlightInfluence));
                color *= lerp(0.82, 1.08, density);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
