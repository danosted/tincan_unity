#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Presentation: draws a fire from its replicated state on every peer. Children named "Flame_*" grow with how full the
    /// fire is and sway softly; "Embers" glows while it burns; the first Light below it brightens and flickers with it.
    /// Nothing shows once the fire is out. The math is in the static functions, which are tested.
    /// </summary>
    public class FireView : MonoBehaviour
    {
        private const string FlamePrefix = "Flame_";
        private const string EmbersName = "Embers";

        [Tooltip("The light's intensity when the fire is full.")]
        [SerializeField, Min(0f)] private float _fullIntensity = 4f;

        private readonly List<(Transform Flame, Vector3 Scale)> _flames = new();
        private IFire? _fire;
        private Light? _light;
        private GameObject? _embers;

        private void Awake()
        {
            _fire = GetComponentInParent<IFire>();
            _light = GetComponentInChildren<Light>(true);
            _embers = transform.Find(EmbersName)?.gameObject;
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(FlamePrefix)) _flames.Add((child, child.localScale));
            }
        }

        private void Update()
        {
            if (_fire == null) return;

            float max = _fire.Config != null ? _fire.Config.MaxBurnSeconds : 0f;
            float fullness = FireProcessor.Fullness(_fire.BurnSecondsLeft, max);
            bool lit = fullness > 0f;
            float time = Time.time;

            for (int i = 0; i < _flames.Count; i++)
            {
                var (flame, scale) = _flames[i];
                flame.gameObject.SetActive(lit);
                if (!lit) continue;
                float height = FlameHeight(fullness, Flicker(time, i));
                flame.localScale = new Vector3(scale.x * (0.8f + 0.2f * height), scale.y * height, scale.z * (0.8f + 0.2f * height));
            }

            if (_embers != null) _embers.SetActive(lit);
            if (_light == null) return;
            _light.enabled = lit;
            _light.intensity = _fullIntensity * LightLevel(fullness, Flicker(time, -1));
        }

        /// <summary>A soft flicker between 0.75 and 1: two slow waves, offset per flame so they do not move together.</summary>
        public static float Flicker(float time, int seed)
        {
            float wave = 0.6f * Mathf.Sin(time * 3.1f + seed * 1.7f) + 0.4f * Mathf.Sin(time * 5.3f + seed * 0.9f);
            return 0.875f + 0.125f * wave;
        }

        /// <summary>A flame's height factor: nothing when out, low on embers, full on a full fire, times the flicker.</summary>
        public static float FlameHeight(float fullness, float flicker) =>
            fullness <= 0f ? 0f : (0.4f + 0.6f * Mathf.Clamp01(fullness)) * flicker;

        /// <summary>The light level: a dying fire still glows, a full one is brightest.</summary>
        public static float LightLevel(float fullness, float flicker) =>
            fullness <= 0f ? 0f : (0.35f + 0.65f * Mathf.Clamp01(fullness)) * flicker;
    }
}
