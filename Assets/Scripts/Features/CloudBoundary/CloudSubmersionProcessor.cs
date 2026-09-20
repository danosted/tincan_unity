#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    public readonly struct CloudSubmersionState
    {
        public float Submersion { get; }
        public float Whiteout { get; }
        public float VapourIntensity { get; }
        public float LightMultiplier { get; }
        public float AudioMuffle { get; }

        public CloudSubmersionState(
            float submersion,
            float whiteout,
            float vapourIntensity,
            float lightMultiplier,
            float audioMuffle)
        {
            Submersion = submersion;
            Whiteout = whiteout;
            VapourIntensity = vapourIntensity;
            LightMultiplier = lightMultiplier;
            AudioMuffle = audioMuffle;
        }
    }

    public class CloudSubmersionProcessor
    {
        public CloudSubmersionState Evaluate(
            float surfaceHeight,
            float altitude,
            float transitionDepth,
            float minLightMultiplier = 0.18f,
            float vapourGain = 1.25f,
            float audioGain = 1.5f)
        {
            float submersion = CalculateSubmersion(surfaceHeight, altitude, transitionDepth);

            float whiteout = Mathf.Clamp01(submersion * submersion);
            float vapourIntensity = Mathf.Clamp01(submersion * vapourGain);
            float lightMultiplier = Mathf.Lerp(1f, minLightMultiplier, whiteout);
            float audioMuffle = Mathf.Clamp01(submersion * audioGain);

            return new CloudSubmersionState(
                submersion,
                whiteout,
                vapourIntensity,
                lightMultiplier,
                audioMuffle);
        }

        public static float CalculateSubmersion(float surfaceHeight, float altitude, float transitionDepth)
        {
            if (transitionDepth <= 0f)
            {
                return 0f;
            }

            float delta = surfaceHeight - altitude;
            return Mathf.Clamp01(delta / transitionDepth);
        }
    }
}
