#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Pure: the wisps from the view's speed and its submersion in the cloud sea (plan .docs/plans/cloud-sea.md, step 3).
    /// Above the sea only speed makes them, lightly; inside it they are there even holding still and grow with depth,
    /// more with speed, and fade into the whiteout fog near full submersion.
    /// </summary>
    public class CloudWispsProcessor
    {
        public CloudWispsFrame Evaluate(float speed, float submersion, CloudWispsConfig config)
        {
            float moving = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(config.SpeedStart, config.SpeedFull, speed));
            submersion = Mathf.Clamp01(submersion);
            float under = submersion > 0f ? Mathf.Lerp(config.UnderFloor, 1f, submersion) : 0f;
            float intensity = Mathf.Clamp01(moving * config.AboveSea + under * Mathf.Lerp(0.6f, 1f, moving));
            if (intensity <= 0f) return new CloudWispsFrame(0f, 0f);

            float engulf = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(config.EngulfStart, 1f, submersion));
            return new CloudWispsFrame(intensity * config.MaxRate, Mathf.Lerp(1f, 0.3f, engulf));
        }

        /// <summary>Whether the view has just entered the cloud: the moment for a burst.</summary>
        public bool Entered(float submersionBefore, float submersionNow) => submersionBefore <= 0f && submersionNow > 0f;
    }
}
