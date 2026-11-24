#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>What the atmosphere applies this frame: the look, with the local view's cloud submersion folded in.</summary>
    public readonly struct AtmosphereFrame
    {
        public AtmosphereFrame(Color sunColour, float sunIntensity, Color ambientSky, Color ambientHorizon, Color ambientGround,
            Color sunwardFog, Color awayFog, float fogDensity, float fogFalloff, float hazeDensity, float maxFog)
        {
            SunColour = sunColour;
            SunIntensity = sunIntensity;
            AmbientSky = ambientSky;
            AmbientHorizon = ambientHorizon;
            AmbientGround = ambientGround;
            SunwardFog = sunwardFog;
            AwayFog = awayFog;
            FogDensity = fogDensity;
            FogFalloff = fogFalloff;
            HazeDensity = hazeDensity;
            MaxFog = maxFog;
        }

        public Color SunColour { get; }
        public float SunIntensity { get; }
        public Color AmbientSky { get; }
        public Color AmbientHorizon { get; }
        public Color AmbientGround { get; }
        public Color SunwardFog { get; }
        public Color AwayFog { get; }
        public float FogDensity { get; }
        public float FogFalloff { get; }
        public float HazeDensity { get; }
        public float MaxFog { get; }

        /// <summary>
        /// A single exponential fog density for shaders that do not take the banded fog (URP Lit, third-party): the
        /// banded density at <paramref name="viewHeight"/> plus the haze.
        /// </summary>
        public float UniformDensityAt(float viewHeight, float baseHeight) =>
            FogDensity * Mathf.Exp(-FogFalloff * Mathf.Max(viewHeight - baseHeight, 0f)) + HazeDensity;
    }

    /// <summary>
    /// Pure: a look plus how deep the view is in the cloud deck gives the frame's light and air. In a cloud the fog
    /// turns toward the whiteout colour and thickens evenly at every height (the bands no longer read from inside),
    /// the haze and the fog's cap rise with it, and the sun and the ambient dim by the cloud's light multiplier.
    /// </summary>
    public sealed class AtmosphereProcessor
    {
        /// <summary>The least of the ambient light left fully inside a cloud, as a share of the light multiplier's dimming.</summary>
        private const float AmbientInCloud = 0.6f;

        /// <summary>How far away the sky is taken to be inside a cloud, in metres: well past what the whiteout fog lets through.</summary>
        public const float SkyReach = 300f;

        /// <summary>
        /// How white the sky and the sky's clouds go in a cloud, 0..1: the whiteout fog over <see cref="SkyReach"/>,
        /// capped like the scene's fog. The sky lies behind everything, so it whitens as much as the farthest scene
        /// surface does; with the raw whiteout it stayed clearer than the fogged cloud around it, and a gap in the
        /// cloud (a rift seen from inside) showed its outline (plan .docs/plans/cloud-sea.md, step 3).
        /// </summary>
        public static float SkyWhiteout(float whiteout, float whiteoutDensity, float maxFog)
        {
            whiteout = Mathf.Clamp01(whiteout);
            float cap = Mathf.Lerp(Mathf.Clamp01(maxFog), 1f, whiteout);
            return Mathf.Min(1f - Mathf.Exp(-whiteout * Mathf.Max(whiteoutDensity, 0f) * SkyReach), cap);
        }

        public AtmosphereFrame Evaluate(in LookSettings look, float whiteout, float lightMultiplier)
        {
            whiteout = Mathf.Clamp01(whiteout);
            lightMultiplier = Mathf.Clamp01(lightMultiplier);
            float ambient = Mathf.Lerp(1f, Mathf.Max(lightMultiplier, AmbientInCloud), whiteout);

            return new AtmosphereFrame(
                look.SunColour,
                look.SunIntensity * lightMultiplier,
                look.AmbientSky * ambient,
                look.AmbientHorizon * ambient,
                look.AmbientGround * ambient,
                Color.Lerp(look.SunwardFog, look.WhiteoutColour, whiteout),
                Color.Lerp(look.AwayFog, look.WhiteoutColour, whiteout),
                Mathf.Lerp(look.FogDensity, look.WhiteoutDensity, whiteout),
                Mathf.Lerp(look.FogFalloff, 0.0001f, whiteout),
                Mathf.Lerp(look.HazeDensity, look.WhiteoutDensity, whiteout),
                Mathf.Lerp(look.MaxFog, 1f, whiteout));
        }
    }
}
