#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipSpirit
{
    /// <summary>
    /// Presentation: the warm light the ship's spirit throws on the room round its hearth, flickering and breathing
    /// slowly with it (ART_DIRECTION.md pillars 4 and 5). Cosmetic and local to each peer: nothing replicates. The wisp
    /// itself (its shape, eyes, blink and gaze) is drawn by its shader, <c>TinCan/Spirit/Flame Wisp</c>. The math is in
    /// the static function, which is tested. Plan: .docs/plans/spirit-talents.md.
    /// </summary>
    public sealed class SpiritHearthLightView : MonoBehaviour
    {
        [Tooltip("The light's intensity at the middle of its flicker.")]
        [SerializeField, Min(0f)] private float _intensity = 3f;

        private Light? _light;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            var position = transform.position;
            _seed = Mathf.Repeat(position.x * 0.37f + position.z * 0.71f, 10f);
        }

        private void Update()
        {
            if (_light != null) _light.intensity = _intensity * Flicker(Time.time, _seed);
        }

        /// <summary>
        /// A hearth's flicker between 0.8 and 1.1: a slow breath under two gentle waves, offset by the seed so two ships'
        /// spirits do not flicker together.
        /// </summary>
        public static float Flicker(float time, float seed)
        {
            float t = time + seed;
            float breath = Mathf.Sin(t * 0.8f);
            float flutter = 0.6f * Mathf.Sin(t * 1.9f) + 0.4f * Mathf.Sin(t * 3.1f + 1.3f);
            return 0.95f + 0.07f * breath + 0.08f * flutter;
        }
    }
}
