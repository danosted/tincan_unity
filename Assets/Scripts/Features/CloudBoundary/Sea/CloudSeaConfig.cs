#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The drawn cloud sea and its holes (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"): the grid that
    /// follows the camera, and the ambient holes the server keeps open near the ships until the weather asks for holes
    /// itself. The surface's shape (swell, hole depth and rim) is the boundary's, on <see cref="CloudBoundaryConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudSeaConfig", menuName = "TinCan/Environment/Cloud Sea Config")]
    public class CloudSeaConfig : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("Width of the square grid around the camera, in metres. Its outer edge fades into the air.")]
        [SerializeField, Min(100f)] private float _gridSize = 3000f;
        [Tooltip("Metres between grid points. The grid snaps to this step, so it never swims.")]
        [SerializeField, Min(1f)] private float _gridStep = 10f;

        [Header("Shells (soft layers above the surface)")]
        [Tooltip("How many see-through layers are drawn above the surface. 0 draws the surface alone. Each is one more pass over the near sea.")]
        [SerializeField, Range(0, 24)] private int _shells = 12;
        [Tooltip("Metres between two layers.")]
        [SerializeField, Min(0.1f)] private float _shellSpacing = 1.5f;
        [Tooltip("Width of the layers' own grid around the camera, in metres; they fade out before its edge.")]
        [SerializeField, Min(50f)] private float _shellGridSize = 1200f;

        [Header("Ambient holes (until the weather opens holes)")]
        [Tooltip("How many holes the server keeps open near the ships. 0 opens none.")]
        [SerializeField, Range(0, ICloudHoles.MaxHoles)] private int _ambientHoles = 4;
        [Tooltip("Seconds between two ambient holes opening.")]
        [SerializeField, Min(0f)] private float _ambientInterval = 8f;
        [SerializeField] private Vector2 _radius = new(25f, 180f);
        [Tooltip("How far from a ship an ambient hole's edge may open, in metres (never under the ship).")]
        [SerializeField] private Vector2 _distance = new(200f, 900f);
        [SerializeField] private Vector2 _openSeconds = new(10f, 20f);
        [Tooltip("Seconds a hole stays fully open.")]
        [SerializeField] private Vector2 _lifeSeconds = new(40f, 90f);
        [SerializeField] private Vector2 _closeSeconds = new(10f, 20f);
        [Tooltip("The fastest an ambient hole drifts, in metres per second.")]
        [SerializeField, Min(0f)] private float _maxDrift = 3f;

        public float GridSize { get => _gridSize; set => _gridSize = value; }
        public float GridStep { get => _gridStep; set => _gridStep = value; }
        public int Shells { get => _shells; set => _shells = value; }
        public float ShellSpacing { get => _shellSpacing; set => _shellSpacing = value; }
        public float ShellGridSize { get => _shellGridSize; set => _shellGridSize = value; }
        public int AmbientHoles { get => _ambientHoles; set => _ambientHoles = value; }
        public float AmbientInterval { get => _ambientInterval; set => _ambientInterval = value; }
        public Vector2 Radius { get => _radius; set => _radius = value; }
        public Vector2 Distance { get => _distance; set => _distance = value; }
        public Vector2 OpenSeconds { get => _openSeconds; set => _openSeconds = value; }
        public Vector2 LifeSeconds { get => _lifeSeconds; set => _lifeSeconds = value; }
        public Vector2 CloseSeconds { get => _closeSeconds; set => _closeSeconds = value; }
        public float MaxDrift { get => _maxDrift; set => _maxDrift = value; }
    }
}
