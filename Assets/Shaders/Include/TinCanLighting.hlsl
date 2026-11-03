// The project's shared light model (.docs/ART_DIRECTION.md, "Light and shading"; plan .docs/plans/look-slice.md, L1):
// wrapped diffuse in two (or three) bands with a tunable edge, shadows that lean toward a tint instead of black,
// ambient from the sky probes, a soft sky-coloured rim, and a soft highlight band for metal (no reflections). Every
// light, the main one and lanterns alike, is shaded the same way. Used by TinCan/Stylized and TinCan/SkyIsland, so
// ships and islands sit in one light.
// The look's shading values and its surface style (plan .docs/plans/artstyle-pipeline-breakdown.md) come from globals
// the Atmosphere feature sets (Features/Atmosphere/LookShaderGlobals.cs). Without it the material's own values hold and
// no surface style keyword is on, so a shader that declares none of them (the clouds) shades exactly as before.
// Include after Core.hlsl, in a forward pass that declares the main-light shadow, additional-light and SSAO keywords;
// to take the surface styles, also `#pragma multi_compile_fragment _ _TINCAN_SURFACE_DRIFT _TINCAN_SURFACE_BRUSH`.
#ifndef TINCAN_LIGHTING_INCLUDED
#define TINCAN_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// How much of the sky's ambient a face keeps where the sun reaches it.
#define TINCAN_AMBIENT_IN_SUN 0.25h

// Globals, not per material (the SRP batcher stays happy): _TinCanShadingOn is 1 while a look applies its shading.
float _TinCanShadingOn;
float4 _TinCanShading;       // x band softness, y bands, z rim, w shadow fill
float4 _TinCanSurfaceParams; // the active surface style's values; what they mean is the style's own

// A surface style changes the albedo (TinCanStyleAlbedo) or N.L before the bands (TinCanStyleNDotL), sampled in object
// space so a pattern rides with a moving ship. One file per style; flat (no keyword) changes nothing.
#if defined(_TINCAN_SURFACE_DRIFT)
#include "Surface/ColourDrift.hlsl"
#elif defined(_TINCAN_SURFACE_BRUSH)
#include "Surface/BrushEdge.hlsl"
#endif

#ifndef TINCAN_STYLE_ALBEDO
half3 TinCanStyleAlbedo(half3 albedo, float3 styleCoord) { return albedo; }
#endif

#ifndef TINCAN_STYLE_NDOTL
half TinCanStyleNDotL(half nDotL, float3 styleCoord) { return nDotL; }
#endif

struct TinCanSurface
{
    half3 albedo;
    float3 normalWS;
    float3 positionWS;
    float4 positionCS;
    half occlusion;  // baked or vertex occlusion, 1 none
    half highlight;  // strength of the metal highlight band, 0 none
    float3 styleCoord; // where a surface style samples its pattern: the object-space position
};

struct TinCanLook
{
    half wrap;          // how far (in N.L) the lit band reaches past the terminator; 0 for shadow casters
    half softness;      // width of the step between the bands, in N.L: 0 a hard line .. 1 a plain gradient
    half3 shadowTint;   // what the shadow band leans toward (the palette's shadow tint)
    half shadowFill;    // how much of the light's colour the shadow band keeps, tinted
    half ambient;       // ambient strength
    half rim;           // rim strength
    half highlightSize; // width of the metal highlight band, 0..1
    half bands;         // 2: lit and shadow; 3: a half-lit band between them
};

// The look's shading values in place of the material's, except those the material keeps (keepOwn x softness, y bands,
// z rim, w shadow fill: 1 keeps the material's). With no look applied the material's values hold.
void TinCanFollowLookShading(inout TinCanLook look, half4 keepOwn)
{
    if (_TinCanShadingOn < 0.5)
    {
        return;
    }

    look.softness = keepOwn.x > 0.5h ? look.softness : (half)_TinCanShading.x;
    look.bands = keepOwn.y > 0.5h ? look.bands : (half)_TinCanShading.y;
    look.rim = keepOwn.z > 0.5h ? look.rim : (half)_TinCanShading.z;
    look.shadowFill = keepOwn.w > 0.5h ? look.shadowFill : (half)_TinCanShading.w;
}

// 0 in the shadow band, 1 in the lit band. The step begins at the terminator (where the surface turns from the light)
// and is as wide as the softness, in units of N.L: a band that crossed the terminator would be cut by the shadow map
// there, in jagged steps. Wrap moves the step that far past the terminator, for surfaces that cast no shadow on
// themselves. With three bands a half-lit band sits between, its two steps half as wide.
half TinCanBand(half nDotL, half wrap, half softness, half bands)
{
    half width = max(softness, 0.001h);
    half start = -wrap;
    if (bands < 2.5h)
    {
        return smoothstep(start, start + width, nDotL);
    }

    width *= 0.5h;
    return 0.5h * smoothstep(start, start + width, nDotL) + 0.5h * smoothstep(start + 0.5h, start + 0.5h + width, nDotL);
}

half TinCanLit(Light light, TinCanSurface surface, TinCanLook look)
{
    half nDotL = TinCanStyleNDotL(dot(surface.normalWS, light.direction), surface.styleCoord);
    return TinCanBand(nDotL, look.wrap, look.softness, look.bands) * light.shadowAttenuation;
}

half3 TinCanLightContribution(Light light, TinCanSurface surface, TinCanLook look, float3 viewWS)
{
    half lit = TinCanLit(light, surface, look);
    half3 band = lerp(look.shadowTint * look.shadowFill, 1.0h, lit);
    half3 colour = surface.albedo * band;

    if (surface.highlight > 0.0h)
    {
        half facing = saturate(dot(surface.normalWS, normalize(light.direction + viewWS)));
        half size = max(look.highlightSize, 0.001h);
        half spot = smoothstep(1.0h - size, 1.0h - size * 0.4h, facing) * surface.highlight * lit;
        colour += lerp(surface.albedo, 1.0h, 0.5h) * spot;
    }

    return colour * light.color * light.distanceAttenuation;
}

half3 TinCanShade(TinCanSurface surface, TinCanLook look)
{
    surface.albedo = TinCanStyleAlbedo(surface.albedo, surface.styleCoord);
    float3 viewWS = GetWorldSpaceNormalizeViewDir(surface.positionWS);
    half3 sky = SampleSH(surface.normalWS);

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(surface.positionWS));
    half3 direct = TinCanLightContribution(mainLight, surface, look, viewWS);
    // The sky fills what the sun does not reach: in the sun's band most of the ambient gives way to the sun, so lit
    // faces sit near their palette colour times the light instead of washing out toward white.
    half sunlit = TinCanLit(mainLight, surface, look) * saturate(Luminance(mainLight.color));
    half3 ambient = sky * look.ambient * surface.occlusion * lerp(1.0h, TINCAN_AMBIENT_IN_SUN, sunlit);

    #if defined(_ADDITIONAL_LIGHTS)
    // Named inputData: the light loop macros read its screen position and world position.
    InputData inputData = (InputData)0;
    inputData.positionWS = surface.positionWS;
    inputData.normalWS = surface.normalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(surface.positionCS);
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        direct += TinCanLightContribution(GetAdditionalLight(lightIndex, surface.positionWS, half4(1, 1, 1, 1)), surface, look, viewWS);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        direct += TinCanLightContribution(GetAdditionalLight(lightIndex, surface.positionWS, half4(1, 1, 1, 1)), surface, look, viewWS);
    LIGHT_LOOP_END
    #endif

    #if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(surface.positionCS));
    ambient *= ao.indirectAmbientOcclusion;
    direct *= ao.directAmbientOcclusion;
    #endif

    half rim = pow(1.0h - saturate(dot(surface.normalWS, viewWS)), 4.0h) * look.rim;
    return surface.albedo * ambient + direct + sky * rim;
}

#endif
