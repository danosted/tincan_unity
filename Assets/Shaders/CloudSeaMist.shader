// The cloud sea as mist near the camera (plan .docs/plans/cloud-sea-fog.md, F4), drawn by CloudMistRendererFeature.
// The mist is the sea's own shape (Include/TinCanCloudSea.hlsl: swell, holes, the web of rifts) softened into a volume:
// its density at a point comes from how far the point lies under the sea's top, from thin Above metres over it to solid
// Below metres under it, so down a rift's wall the edge is a gradient instead of a line. A thinner tail hangs from the
// rim into rifts and holes (Spill); heaps and a drifting detail noise break the top. Coloured with the sea's own look
// (Include/TinCanCloudLook.hlsl, from globals) and seen through the air like everything else.
// Pass 0 marches at the tier's resolution: only through the slab the mist can fill, up to the tier's range, with steps
// finer near the eye; the shading inputs (lit, hollow, thin) are gathered along the ray and shaded once. Pass 1 lays the
// result over the camera's colour, blurred a little to smooth the march's grain and upsampled by depth so the mist does
// not bleed over the ship's edges.
// The material holds the look; the tier's values (_Mist*) are set by the renderer feature each frame.
Shader "TinCan/Environment/Cloud Sea Mist"
{
    Properties
    {
        [Header(Shape)]
        _TopLift ("Top Lift (m the mist's top sits above the game's surface)", Range(-30, 30)) = 0
        _Above ("Above (m over the top where the mist thins out)", Range(0.5, 40)) = 8
        _Below ("Below (m under the top until the mist is solid)", Range(0.5, 60)) = 14
        _Density ("Density (per m where solid)", Range(0.005, 1)) = 0.12
        _HeapScale ("Heaps (m)", Float) = 70
        _HeapHeight ("Heap Height (m)", Range(0, 40)) = 10

        [Header(Holes and Rifts)]
        _HoleDrop ("Hole Drop (m the top sinks into a hole or rift)", Range(0, 200)) = 60
        _Spill ("Spill (mist hanging from the rim into a rift)", Range(0, 1)) = 0.35
        _SpillDepth ("Spill Depth (m it hangs down)", Range(1, 200)) = 40

        [Header(Detail)]
        _DetailScale ("Detail (m)", Float) = 18
        _DetailStrength ("Detail Strength (how much it frays the edges)", Range(0, 1)) = 0.6
        _Coverage ("Coverage (0 even mist .. clear gaps between puffs)", Range(0, 0.9)) = 0.2
        _Wind ("Wind (m/s, x and z)", Vector) = (2.5, 0.8, 0, 0)

        [Header(Light)]
        _Hollow ("Hollow (deep mist leans to the sea's hollow colour)", Range(0, 1)) = 0.6
        _Absorb ("Absorb (how fast the sun dims inside)", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        ENDHLSL

        Pass
        {
            Name "March"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMarch
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Include/TinCanLighting.hlsl"
            #include "Include/TinCanAtmosphere.hlsl"
            #include "Include/TinCanCloudSea.hlsl"
            #include "Include/TinCanCloudLook.hlsl"

            float _TopLift;
            float _Above;
            float _Below;
            float _Density;
            float _HeapScale;
            float _HeapHeight;
            float _HoleDrop;
            float _Spill;
            float _SpillDepth;
            float _DetailScale;
            float _DetailStrength;
            float _Coverage;
            float4 _Wind;
            float _Hollow;
            float _Absorb;

            // The tier (CloudMistRendererFeature).
            float _MistSteps;
            float _MistLightSteps;
            float _MistOctaves;
            float _MistShadows;
            float _MistRange;
            float _MistFrame;

            // A different offset each pixel and frame, so few steps blur into grain instead of bands.
            float MistJitter(float2 pixel)
            {
                pixel += 5.588238 * _MistFrame;
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            // The mist's density at p, 0..1, and how many metres under the mist's top p lies (negative above it).
            float MistDensity(float3 p, float time, out float below)
            {
                float2 xz = p.xz;
                float cut = TinCanCloudHoleCut(xz);
                float rim = _TinCanCloudSea.y + _TopLift + TinCanCloudSwell(xz);
                float heaps = TinCanCloudBillow(float3(xz / max(_HeapScale, 0.01), time * 0.01));
                float top = rim + (heaps - 0.5) * _HeapHeight * (1.0 - cut) - cut * _HoleDrop;
                below = top - p.y;

                float band = smoothstep(-_Above, _Below, below);
                // The spill: from the rim's height down into the opening, thinning as it goes.
                float hang = max(rim - p.y, 0.0);
                float spill = p.y < rim + _Above ? _Spill * smoothstep(0.05, 0.5, cut) * exp(-hang / max(_SpillDepth, 1.0)) : 0.0;
                float density = max(band, spill);

                int octaves = (int)_MistOctaves;
                if (octaves > 0 && density > 0.0)
                {
                    float3 q = (p - float3(_Wind.x, 0.0, _Wind.y) * time) / max(_DetailScale, 0.01);
                    float noise = 0.0;
                    float weight = 0.5;
                    float total = 0.0;
                    for (int o = 0; o < 3; o++)
                    {
                        if (o >= octaves) break;
                        noise += TinCanCloudNoise(q) * weight;
                        total += weight;
                        q = q * 2.03 + 17.1;
                        weight *= 0.5;
                    }

                    noise /= max(total, 1e-3);
                    // Frays the thin parts most: the solid core keeps, the edges break into puffs.
                    density = saturate(density * (1.0 + _DetailStrength) - (1.0 - noise) * _DetailStrength);
                }

                return smoothstep(_Coverage, 1.0, density);
            }

            half4 FragMarch(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float raw = SampleSceneDepth(uv);
                float3 eye = GetCameraPositionWS();
                float3 scene = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float sceneDistance = length(scene - eye);
                float3 dir = (scene - eye) / max(sceneDistance, 1e-4);
                float time = _TinCanCloudSea.x;

                // The slab the mist can fill: over the highest heap on the highest swell, down to the spill under a hole.
                float rimBase = _TinCanCloudSea.y + _TopLift;
                float swell = abs(_TinCanCloudSea.z);
                float yTop = rimBase + swell + _HeapHeight * 0.5 + _Above;
                float yBottom = rimBase - swell - _HeapHeight * 0.5 - _HoleDrop - _SpillDepth;
                float t0 = 0.0;
                float t1 = min(sceneDistance, _MistRange);
                if (abs(dir.y) > 1e-5)
                {
                    float ta = (yBottom - eye.y) / dir.y;
                    float tb = (yTop - eye.y) / dir.y;
                    t0 = max(t0, min(ta, tb));
                    t1 = min(t1, max(ta, tb));
                }
                else if (eye.y < yBottom || eye.y > yTop)
                {
                    return 0.0h;
                }

                if (t1 <= t0) return 0.0h;

                int steps = max((int)_MistSteps, 1);
                int lightSteps = (int)_MistLightSteps;
                float jitter = MistJitter(input.positionCS.xy);
                float span = t1 - t0;
                float fadeStart = _MistRange * 0.6;
                float3 sun = _MainLightPosition.xyz;
                float lightStep = max(_Below * 0.5, 0.5);

                float transmittance = 1.0;
                float weightSum = 0.0;
                float litSum = 0.0;
                float hollowSum = 0.0;
                float thinSum = 0.0;
                float distanceSum = 0.0;
                for (int i = 0; i < 64; i++)
                {
                    if (i >= steps) break;
                    // Steps finer near the eye: t grows with the square of the step's place.
                    float from = (float)i / steps;
                    float to = (float)(i + 1) / steps;
                    float at = (i + jitter) / steps;
                    float t = t0 + span * at * at;
                    float dt = span * (to * to - from * from);
                    float3 p = eye + dir * t;

                    float below;
                    float density = MistDensity(p, time, below) * (1.0 - smoothstep(fadeStart, _MistRange, t));
                    if (density <= 1e-3) continue;

                    float alpha = 1.0 - exp(-density * _Density * dt);
                    float lit;
                    if (lightSteps > 0)
                    {
                        float optical = 0.0;
                        for (int k = 1; k <= 6; k++)
                        {
                            if (k > lightSteps) break;
                            float ignore;
                            optical += MistDensity(p + sun * (lightStep * k), time, ignore) * lightStep;
                        }

                        lit = exp(-optical * _Density * _Absorb);
                    }
                    else
                    {
                        lit = exp(-max(below, 0.0) / max(_Below, 0.01) * _Absorb);
                    }

                    if (_MistShadows > 0.5) lit *= MainLightRealtimeShadow(TransformWorldToShadowCoord(p));

                    float weight = transmittance * alpha;
                    litSum += lit * weight;
                    hollowSum += saturate(below / (_Below * 1.5)) * weight;
                    thinSum += (1.0 - density) * weight;
                    distanceSum += t * weight;
                    weightSum += weight;
                    transmittance *= 1.0 - alpha;
                    if (transmittance < 0.01) break;
                }

                if (weightSum <= 1e-5) return 0.0h;

                half lit = (half)(litSum / weightSum);
                half hollow = (half)(hollowSum / weightSum * _Hollow);
                half thin = (half)(thinSum / weightSum);
                float3 position = eye + dir * (distanceSum / weightSum);

                TinCanCloudLook look = TinCanCloudLookFromGlobals();
                half3 colour = TinCanCloudShadeLit(look, lit, SampleSH(float3(0, 1, 0)), 0.0h, -dir, hollow, thin);
                colour = TinCanWeatherShade(colour, position);
                colour = TinCanNightShade(colour);
                half opacity = (half)(1.0 - transmittance);
                return TinCanAtmospherePremultiplied(half4(colour * opacity, opacity), position);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            float4 _MistSize; // the march's width, height, and their inverses

            // The march's 4x4 texels around this pixel, blended by a soft bell over their distance (about a texel wide),
            // which smooths the march's per-pixel grain into an even mist; each also weighed down by how far its depth is
            // from this pixel's, so the mist behind the ship's rail or an island's edge does not smear over it.
            half4 FragComposite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 position = uv * _MistSize.xy - 0.5;
                float2 corner = floor(position);
                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                half4 sum = 0.0h;
                float weightSum = 0.0;
                [unroll] for (int k = 0; k < 16; k++)
                {
                    float2 texel = corner + float2((k & 3) - 1, (k >> 2) - 1);
                    float2 apartXY = texel - position;
                    float bell = exp(-dot(apartXY, apartXY) * 0.5);
                    float2 tapUv = (texel + 0.5) * _MistSize.zw;
                    float tapDepth = LinearEyeDepth(SampleSceneDepth(tapUv), _ZBufferParams);
                    float apart = abs(tapDepth - depth) / max(depth, 1e-3);
                    float weight = bell / (1.0 + apart * 100.0) + 1e-6;
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, tapUv, 0) * weight;
                    weightSum += weight;
                }

                return sum / weightSum;
            }
            ENDHLSL
        }
    }
}
