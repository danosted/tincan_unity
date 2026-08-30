Shader "Hidden/TinCan/Volumetric Clouds"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "RayMarch"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment RayMarchFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_CloudHeightMap);
            SAMPLER(sampler_CloudHeightMap);

            float _CloudEnabled;
            float4 _CloudHeightMapCenter;
            float4 _CloudLayerParams;
            float4 _CloudShapeParams;
            float4 _CloudLightParams;
            float4 _CloudWind;
            half4 _CloudBaseColor;
            half4 _CloudSunColor;
            half4 _CloudAmbientSky;
            half4 _CloudAmbientGround;
            float _CloudFadeStart;
            int _CloudStepCount;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

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

                float lower = lerp(
                    lerp(Hash31(cell), Hash31(cell + float3(1, 0, 0)), fraction.x),
                    lerp(Hash31(cell + float3(0, 1, 0)), Hash31(cell + float3(1, 1, 0)), fraction.x),
                    fraction.y);
                float upper = lerp(
                    lerp(Hash31(cell + float3(0, 0, 1)), Hash31(cell + float3(1, 0, 1)), fraction.x),
                    lerp(Hash31(cell + float3(0, 1, 1)), Hash31(cell + 1.0), fraction.x),
                    fraction.y);
                return lerp(lower, upper, fraction.z);
            }

            float FractalNoise(float3 samplePosition)
            {
                float noise = ValueNoise(samplePosition) * 0.625;
                noise += ValueNoise(samplePosition * 2.03 + 11.7) * 0.25;
                noise += ValueNoise(samplePosition * 4.11 - 7.3) * 0.125;
                return noise;
            }

            float SampleSurfaceHeight(float2 worldPosition)
            {
                float2 uv = (worldPosition - _CloudHeightMapCenter.xy) * _CloudHeightMapCenter.w + 0.5;
                return SAMPLE_TEXTURE2D_LOD(_CloudHeightMap, sampler_CloudHeightMap, saturate(uv), 0).r;
            }

            float SampleCloudDensityHeight(float3 worldPosition, out float heightFraction)
            {
                float2 offsetFromCamera = worldPosition.xz - _WorldSpaceCameraPos.xz;
                float curvature = dot(offsetFromCamera, offsetFromCamera) / (2.0 * _CloudLayerParams.z);
                float layerBottom = SampleSurfaceHeight(worldPosition.xz) - _CloudLayerParams.y - curvature;
                heightFraction = (worldPosition.y - layerBottom) / _CloudLayerParams.x;
                if (heightFraction <= 0.0 || heightFraction >= 1.0)
                {
                    return 0.0;
                }

                float lowerFade = smoothstep(0.0, 0.12, heightFraction);
                float upperFade = 1.0 - smoothstep(0.62, 1.0, heightFraction);
                float3 windOffset = float3(_CloudWind.x, 0.0, _CloudWind.y) * _Time.y;
                float3 noisePosition = (worldPosition + windOffset) * _CloudShapeParams.z;
                noisePosition += _CloudShapeParams.w * 0.0137;
                float shape = FractalNoise(noisePosition);
                float threshold = 1.0 - _CloudShapeParams.x * 0.75;
                float coverageDensity = smoothstep(threshold, min(1.0, threshold + 0.24), shape);
                return coverageDensity * lowerFade * upperFade * _CloudShapeParams.y;
            }

            float SampleCloudDensity(float3 worldPosition)
            {
                float heightFraction;
                return SampleCloudDensityHeight(worldPosition, heightFraction);
            }

            // Anisotropic forward scattering, normalised so a fully forward-facing sample stays near unity.
            float HenyeyGreenstein(float cosAngle, float eccentricity)
            {
                float squaredEccentricity = eccentricity * eccentricity;
                float denominator = 1.0 + squaredEccentricity - 2.0 * eccentricity * cosAngle;
                return (1.0 - squaredEccentricity) / max(pow(abs(denominator), 1.5), 0.0001);
            }

            // Dual lobe keeps a back-scattering response so clouds facing away from the sun stay readable
            // instead of collapsing to black.
            float CloudPhase(float cosAngle, float eccentricity)
            {
                float forward = HenyeyGreenstein(cosAngle, eccentricity);
                float backward = HenyeyGreenstein(cosAngle, -eccentricity * 0.35);
                return max(forward, backward * 0.6);
            }

            // Beer-Lambert extinction of sunlight reaching a sample, marched towards the main light.
            float SampleSunTransmittance(float3 worldPosition, float3 lightDirection)
            {
                const int lightStepCount = 5;
                float stepLength = _CloudLayerParams.x * 0.16;
                float opticalDepth = 0.0;
                float3 marchPosition = worldPosition;

                [unroll]
                for (int lightStep = 0; lightStep < lightStepCount; lightStep++)
                {
                    marchPosition += lightDirection * stepLength;
                    opticalDepth += SampleCloudDensity(marchPosition) * stepLength;
                }

                return exp(-opticalDepth * _CloudLightParams.x * 0.01);
            }

            float ResolveSceneDistance(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
#if UNITY_REVERSED_Z
                if (rawDepth <= 0.0001)
#else
                if (rawDepth >= 0.9999)
#endif
                {
                    return _CloudLayerParams.w;
                }

                float3 scenePosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                return min(distance(scenePosition, _WorldSpaceCameraPos), _CloudLayerParams.w);
            }

            bool ResolveMarchInterval(float2 uv, float3 rayDirection, out float startDistance, out float endDistance)
            {
                float sceneDistance = ResolveSceneDistance(uv);
                float centerSurfaceHeight = SampleSurfaceHeight(_WorldSpaceCameraPos.xz);
                float maximumCurvature = _CloudLayerParams.w * _CloudLayerParams.w / (2.0 * _CloudLayerParams.z);
                float lowerHeight = centerSurfaceHeight - _CloudLayerParams.y - maximumCurvature - 32.0;
                float upperHeight = centerSurfaceHeight - _CloudLayerParams.y + _CloudLayerParams.x + 32.0;

                startDistance = 0.0;
                endDistance = sceneDistance;
                if (abs(rayDirection.y) > 0.0001)
                {
                    float lowerIntersection = (lowerHeight - _WorldSpaceCameraPos.y) / rayDirection.y;
                    float upperIntersection = (upperHeight - _WorldSpaceCameraPos.y) / rayDirection.y;
                    startDistance = max(0.0, min(lowerIntersection, upperIntersection));
                    endDistance = min(sceneDistance, max(lowerIntersection, upperIntersection));
                }

                return endDistance > startDistance;
            }

            half4 RayMarchFragment(Varyings input) : SV_Target
            {
                if (_CloudEnabled < 0.5)
                {
                    return 0.0;
                }

                float3 farPosition = ComputeWorldSpacePosition(input.uv, UNITY_RAW_FAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                float3 rayDirection = normalize(farPosition - _WorldSpaceCameraPos);
                float startDistance;
                float endDistance;
                if (!ResolveMarchInterval(input.uv, rayDirection, startDistance, endDistance))
                {
                    return 0.0;
                }

                float marchDistance = endDistance - startDistance;
                float stepLength = marchDistance / max(_CloudStepCount, 1);
                float distanceAlongRay = startDistance + stepLength * 0.5;
                float transmittance = 1.0;
                half3 accumulatedColor = 0.0;

                Light mainLight = GetMainLight();
                float3 lightDirection = mainLight.direction;
                half3 sunColor = _CloudSunColor.rgb * mainLight.color;
                // Environment lighting keeps shadowed cloud interiors tied to the skybox instead of
                // collapsing to black. It arrives as an explicit global because SampleSH reads
                // UnityPerDraw, which is never bound for a full-screen procedural draw.
                half3 skyAmbient = max(_CloudAmbientSky.rgb, 0.0) * _CloudLightParams.w;
                half3 groundAmbient = max(_CloudAmbientGround.rgb, 0.0) * _CloudLightParams.w;
                float phase = CloudPhase(dot(rayDirection, lightDirection), _CloudLightParams.y);
                float fadeBegin = _CloudLayerParams.w * _CloudFadeStart;

                [loop]
                for (int sampleIndex = 0; sampleIndex < _CloudStepCount; sampleIndex++)
                {
                    float3 samplePosition = _WorldSpaceCameraPos + rayDirection * distanceAlongRay;
                    float heightFraction;
                    float density = SampleCloudDensityHeight(samplePosition, heightFraction);
                    density *= 1.0 - smoothstep(fadeBegin, _CloudLayerParams.w, distanceAlongRay);
                    if (density > 0.001)
                    {
                        float sampleAlpha = 1.0 - exp(-density * stepLength * 0.018);
                        float sunTransmittance = SampleSunTransmittance(samplePosition, lightDirection);
                        float powder = 1.0 - exp(-density * 4.0);
                        float powderTerm = lerp(1.0, powder, _CloudLightParams.z);
                        half3 directColor = sunColor * (sunTransmittance * phase * powderTerm);
                        half3 ambientColor = lerp(groundAmbient, skyAmbient, saturate(heightFraction));
                        half3 sampleColor = _CloudBaseColor.rgb * (directColor + ambientColor);
                        accumulatedColor += transmittance * sampleAlpha * sampleColor;
                        transmittance *= 1.0 - sampleAlpha;
                        if (transmittance < 0.01)
                        {
                            break;
                        }
                    }

                    distanceAlongRay += stepLength;
                }

                return half4(accumulatedColor, 1.0 - transmittance);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CloudSourceColor);
            SAMPLER(sampler_CloudSourceColor);
            TEXTURE2D(_CloudTexture);
            SAMPLER(sampler_CloudTexture);
            float _CloudDebug;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 CompositeFragment(Varyings input) : SV_Target
            {
                half4 sourceColor = SAMPLE_TEXTURE2D(_CloudSourceColor, sampler_CloudSourceColor, input.uv);
                half4 cloudColor = SAMPLE_TEXTURE2D(_CloudTexture, sampler_CloudTexture, input.uv);
                if (_CloudDebug > 0.5)
                {
                    return half4(sourceColor.rgb, 1.0);
                }

                if (_CloudDebug < -0.5)
                {
                    return half4(cloudColor.aaa, 1.0);
                }

                sourceColor.rgb = cloudColor.rgb + sourceColor.rgb * (1.0 - cloudColor.a);
                return sourceColor;
            }
            ENDHLSL
        }
    }
}
