#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.CloudBoundary.Atmosphere
{
    /// <summary>
    /// What a weather does to the volumetric clouds at its full weight (a storm's overcast closing in). The cloud director
    /// blends its own values toward these by the weather's weight, in Play only, so the scene's tuning is never written
    /// over. The wind speed is left alone: the clouds' place is time times speed, so changing it would jump them.
    /// Plan: .docs/plans/storm-crossing.md (ST1).
    /// </summary>
    [Serializable]
    public struct CloudWeatherTarget
    {
        [Tooltip("Cloud density at full weight (the director's Density Multiplier).")]
        [Range(0f, 1f)] public float Density;
        [Tooltip("Shape factor at full weight: lower covers more of the sky.")]
        [Range(0f, 1f)] public float ShapeFactor;
        [Tooltip("Opacity variation at full weight: 0 makes every cloud as thick as the rest (an even overcast).")]
        [Range(0f, 1f)] public float OpacityVariation;
        [Tooltip("The sky's light inside the clouds at full weight (Ambient Light Probe Dimmer): lower is darker cores.")]
        [Range(0f, 10f)] public float AmbientDimmer;
    }

    /// <summary>A weather the volumetric clouds blend toward (the storm). Read every frame in Play; presentation only.</summary>
    public interface ICloudWeather
    {
        float Weight { get; }
        CloudWeatherTarget Clouds { get; }
    }
}
