// The look of the cloud sea, shared with the volumetric sky (plan .docs/plans/cloud-sea.md; ART_DIRECTION.md, "Sky and
// clouds"): the noise the sea's heaps are made of, and the light: banded sun, cream lit heaps, cool hollows, sky fill in
// shadow, a sky-coloured rim at grazing angles, light through thin tops toward the sun. Shaders/CloudSeaSwell.shader
// includes it after URP's Core.hlsl and TinCanLighting.hlsl and declares the material properties TINCAN_CLOUD_LOOK
// reads. The vendored volumetric clouds (ThirdParty/VolumetricCloudsURP, "TinCan: the painted look") read the same look
// from globals (TinCanCloudLookFromGlobals), so tuning the sea tunes the sky.
#ifndef TINCAN_CLOUD_LOOK_INCLUDED
#define TINCAN_CLOUD_LOOK_INCLUDED

float TinCanCloudHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float TinCanCloudNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(TinCanCloudHash(cell), TinCanCloudHash(cell + float3(1, 0, 0)), f.x), lerp(TinCanCloudHash(cell + float3(0, 1, 0)), TinCanCloudHash(cell + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(TinCanCloudHash(cell + float3(0, 0, 1)), TinCanCloudHash(cell + float3(1, 0, 1)), f.x), lerp(TinCanCloudHash(cell + float3(0, 1, 1)), TinCanCloudHash(cell + 1), f.x), f.y),
        f.z);
}

// Billow noise: rounded heaps with sharp creases between them, 0 in a crease .. 1 on a heap.
float TinCanCloudBillow(float3 p)
{
    return 1.0 - abs(TinCanCloudNoise(p) * 2.0 - 1.0);
}

struct TinCanCloudLook
{
    half3 lit;
    half3 shadow;
    half3 hollow;
    half hollowDarken;
    half sunTint;
    half ambient;
    half wrap;
    half bandSoftness;
    half bands;
    half rim;
    half rimPower;
    half silverLining;
    half silverFocus;
    half translucency;
};

// The look from the material properties both shaders declare.
#define TINCAN_CLOUD_LOOK(look) \
    TinCanCloudLook look; \
    look.lit = _LitColour.rgb; look.shadow = _ShadowColour.rgb; look.hollow = _HollowColour.rgb; \
    look.hollowDarken = _HollowDarken; look.sunTint = _SunTint; look.ambient = _Ambient; \
    look.wrap = _Wrap; look.bandSoftness = _BandSoftness; look.bands = _Bands; \
    look.rim = _Rim; look.rimPower = _RimPower; look.silverLining = _SilverLining; look.silverFocus = _SilverFocus; \
    look.translucency = _Translucency;

// The cloud's colour from how lit it is. lit: 0 shadow .. 1 sunlit, already banded. skyFill: the sky's light on it.
// grazing: 0 facing the camera .. 1 edge-on. hollow: 0 on a heap .. 1 deep between heaps or underneath. thin: 0 deep
// in the cloud .. 1 its thin tops and edges, which take more light and let the sun through.
half3 TinCanCloudShadeLit(TinCanCloudLook look, half lit, half3 skyFill, half grazing, float3 view, half hollow, half thin)
{
    Light sun = GetMainLight();
    lit = saturate(lit + thin * 0.35h);
    half3 lightColour = lerp(1.0h, sun.color, look.sunTint);
    half3 colour = lerp(look.shadow, look.lit * lightColour, lit);
    colour += skyFill * look.ambient * (1.0h - lit);
    colour = lerp(colour, look.hollow * lightColour, look.hollowDarken * hollow);

    // Grazing angles take the sky; toward the sun, light through the thin tops and a silver lining.
    colour = lerp(colour, SampleSH(float3(0, 1, 0)) + sun.color * 0.25h, grazing * look.rim);
    half toward = (half)pow(saturate(dot(-view, sun.direction)), look.silverFocus);
    colour += sun.color * toward * (grazing * look.silverLining + thin * look.translucency);
    return colour;
}

// The cloud's colour at a point of a surface (the sea): lit by the banded sun on its normal.
half3 TinCanCloudShade(TinCanCloudLook look, float3 normal, float3 view, half hollow, half thin)
{
    Light sun = GetMainLight();
    half lit = TinCanBand((half)dot(normal, sun.direction), look.wrap, look.bandSoftness, look.bands);
    half grazing = (half)pow(1.0 - saturate(dot(normal, view)), look.rimPower);
    return TinCanCloudShadeLit(look, lit, SampleSH(normal), grazing, view, hollow, thin);
}

// The sea's look as globals (CloudSeaShaderGlobals.ApplyLook, each frame the sea draws), for shaders that have no
// material of their own with these knobs: the volumetric sky's painted mode. _TinCanCloudLookOn is 0 without a sea.
float _TinCanCloudLookOn;
float4 _TinCanCloudLookLit;    // rgb lit, a hollow darken
float4 _TinCanCloudLookShadow; // rgb shadow, a sun tint
float4 _TinCanCloudLookHollow; // rgb hollow, a ambient
float4 _TinCanCloudLookLight;  // x wrap, y band softness, z bands, w rim
float4 _TinCanCloudLookSilver; // x rim power, y silver lining, z silver focus, w translucency

TinCanCloudLook TinCanCloudLookFromGlobals()
{
    TinCanCloudLook look;
    look.lit = _TinCanCloudLookLit.rgb;
    look.shadow = _TinCanCloudLookShadow.rgb;
    look.hollow = _TinCanCloudLookHollow.rgb;
    look.hollowDarken = _TinCanCloudLookLit.a;
    look.sunTint = _TinCanCloudLookShadow.a;
    look.ambient = _TinCanCloudLookHollow.a;
    look.wrap = _TinCanCloudLookLight.x;
    look.bandSoftness = _TinCanCloudLookLight.y;
    look.bands = _TinCanCloudLookLight.z;
    look.rim = _TinCanCloudLookLight.w;
    look.rimPower = _TinCanCloudLookSilver.x;
    look.silverLining = _TinCanCloudLookSilver.y;
    look.silverFocus = _TinCanCloudLookSilver.z;
    look.translucency = _TinCanCloudLookSilver.w;
    return look;
}

#endif
