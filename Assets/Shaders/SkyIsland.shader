// Sky islands (TinCan.Features.SkyIslands). The procedural island mesh carries its masks in vertex colours
// (SkyIslandMeshData): r grass, g ambient occlusion, b a strata value per ring, a how far down the rock (0 top, 1 apex).
// Grass where the mesh says top and the surface faces up; warm layered rock elsewhere, darker toward the apex.
// Main light with wrapped diffuse and shadows, ambient from the probes, fog. Depth passes so clouds and SSAO see it.
Shader "TinCan/SkyIsland"
{
    Properties
    {
        _GrassColor ("Grass", Color) = (0.36, 0.55, 0.20, 1)
        _GrassDryColor ("Grass (dry patches)", Color) = (0.55, 0.60, 0.28, 1)
        _RockLight ("Rock (light)", Color) = (0.63, 0.47, 0.34, 1)
        _RockDark ("Rock (dark)", Color) = (0.40, 0.28, 0.21, 1)
        _RockDeep ("Rock (deep, toward the apex)", Color) = (0.24, 0.18, 0.16, 1)
        _StrataPerMetre ("Strata bands per metre", Float) = 0.12
        _PatchScale ("Grass patch scale", Float) = 0.04
        _Wrap ("Light wrap", Range(0, 1)) = 0.35
        _AmbientBoost ("Ambient boost", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _GrassColor;
            half4 _GrassDryColor;
            half4 _RockLight;
            half4 _RockDark;
            half4 _RockDeep;
            float _StrataPerMetre;
            float _PatchScale;
            half _Wrap;
            half _AmbientBoost;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Cheap smooth value noise for colour variation (not geometry, so float differences do not matter).
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                half grassMask = saturate(input.color.r) * smoothstep(0.35, 0.7, normalWS.y);

                float patches = ValueNoise(input.positionWS.xz * _PatchScale) * 0.65
                              + ValueNoise(input.positionWS.xz * _PatchScale * 4.0) * 0.35;
                half3 grass = lerp(_GrassColor.rgb, _GrassDryColor.rgb, smoothstep(0.45, 0.8, patches));

                float band = frac(input.positionWS.y * _StrataPerMetre + input.color.b * 0.35
                                + ValueNoise(input.positionWS.xz * 0.05) * 0.4);
                half layer = smoothstep(0.15, 0.55, band) * 0.65 + input.color.b * 0.35;
                half3 rock = lerp(_RockDark.rgb, _RockLight.rgb, layer);
                rock = lerp(rock, _RockDeep.rgb, input.color.a * 0.7);

                half3 albedo = lerp(rock, grass, grassMask);
                half occlusion = input.color.g;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half diffuse = saturate((dot(normalWS, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                half3 direct = mainLight.color * diffuse * mainLight.shadowAttenuation;
                half3 ambient = SampleSH(normalWS) * _AmbientBoost;

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    ambient *= ao.indirectAmbientOcclusion;
                    direct *= ao.directAmbientOcclusion;
                #endif

                half3 color = albedo * (direct + ambient * occlusion);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
