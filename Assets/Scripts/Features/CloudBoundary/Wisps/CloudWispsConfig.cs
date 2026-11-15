#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The wisps that stream past the camera (plan .docs/plans/cloud-sea.md, step 3): how speed and depth in the cloud
    /// sea set how many there are, and their size and streak. Presentation only (<see cref="CloudWispsPresenter"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "CloudWispsConfig", menuName = "TinCan/Environment/Cloud Wisps Config")]
    public class CloudWispsConfig : ScriptableObject
    {
        [Header("Speed")]
        [Tooltip("Speed of the view (m/s) below which there are no wisps from speed alone: walking pace.")]
        [SerializeField, Min(0f)] private float _speedStart = 4f;
        [Tooltip("Speed of the view (m/s) at which the wisps are at their fullest and streak the most.")]
        [SerializeField, Min(0.1f)] private float _speedFull = 40f;
        [Tooltip("Intensity from speed alone, above the cloud sea (0..1): a light stream of streaks when moving.")]
        [SerializeField, Range(0f, 1f)] private float _aboveSea = 0.3f;

        [Header("Depth")]
        [Tooltip("Intensity just inside the cloud, holding still (0..1); it rises to full with depth. Depth weighs more than speed.")]
        [SerializeField, Range(0f, 1f)] private float _underFloor = 0.5f;
        [Tooltip("Submersion (0..1) from which the wisps fade into the whiteout fog: at full, the fog alone is left.")]
        [SerializeField, Range(0f, 1f)] private float _engulfStart = 0.85f;
        [Tooltip("Wisps thrown out at once on entering the cloud.")]
        [SerializeField, Min(0)] private int _burst = 40;

        [Header("Wisps")]
        [Tooltip("Wisps per second at full intensity.")]
        [SerializeField, Min(0f)] private float _maxRate = 120f;
        [Tooltip("A wisp's size, in metres (smallest, largest).")]
        [SerializeField] private Vector2 _size = new(0.4f, 1.6f);
        [Tooltip("How long a wisp lives, in seconds.")]
        [SerializeField, Min(0.1f)] private float _lifetime = 1.2f;
        [Tooltip("How far around the view wisps appear, in metres (the nearest are half as far).")]
        [SerializeField, Min(1f)] private float _spawnRadius = 14f;
        [Tooltip("How much a wisp stretches along the travel per m/s of the view's speed.")]
        [SerializeField, Min(0f)] private float _stretch = 0.04f;

        public float SpeedStart { get => _speedStart; set => _speedStart = value; }
        public float SpeedFull { get => _speedFull; set => _speedFull = value; }
        public float AboveSea { get => _aboveSea; set => _aboveSea = value; }
        public float UnderFloor { get => _underFloor; set => _underFloor = value; }
        public float EngulfStart { get => _engulfStart; set => _engulfStart = value; }
        public int Burst { get => _burst; set => _burst = value; }
        public float MaxRate { get => _maxRate; set => _maxRate = value; }
        public Vector2 Size { get => _size; set => _size = value; }
        public float Lifetime { get => _lifetime; set => _lifetime = value; }
        public float SpawnRadius { get => _spawnRadius; set => _spawnRadius = value; }
        public float Stretch { get => _stretch; set => _stretch = value; }
    }
}
