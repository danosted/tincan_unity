#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Presentation Layer: the in-world fuel gauge beside the helm (model: Assets/Models/ShipComponents/FuelGauge).
    /// Polls the ship's fuel tank every frame (like AirshipDoor), so late joiners and clients need no extra sync;
    /// swings the indicator around its axle between the empty and full angles, relative to the indicator's imported
    /// (neutral) pose, and tints the optional lamp when the tank is dry. Sits anywhere under the airship prefab.
    /// </summary>
    public class FuelGaugeView : MonoBehaviour
    {
        /// <summary>Indicator node names in lookup order: the fuel_gauge.fbx needle, then the legacy placeholder.</summary>
        private static readonly string[] NeedleNames = { "FuelGauge_Indicator", "Needle" };
        private const string LampName = "Lamp";

        [Tooltip("Indicator angle for an empty tank, in degrees around Needle Axis. fuel_gauge.fbx: +65 points at E.")]
        [SerializeField] private float _emptyAngle = 65f;
        [Tooltip("Indicator angle for a full tank. fuel_gauge.fbx: -65 points at F. Swap both signs if the needle runs backwards.")]
        [SerializeField] private float _fullAngle = -65f;
        [Tooltip("Needle axle in the indicator's local space. fuel_gauge.fbx imports with the dial normal on local Y.")]
        [SerializeField] private Vector3 _needleAxis = Vector3.up;
        [SerializeField] private float _needleSmoothing = 6f;
        [SerializeField] private Color _lampOkColor = new(0.1f, 0.6f, 0.1f);
        [SerializeField] private Color _lampEmptyColor = new(0.9f, 0.1f, 0.1f);

        private Transform? _needle;
        private Quaternion _neutralRotation = Quaternion.identity;
        private Renderer? _lamp;
        private IFuelTank? _tank;
        private float _currentAngle;

        private void Awake()
        {
            _needle = FindNeedle(transform);
            if (_needle != null) _neutralRotation = _needle.localRotation;
            _lamp = transform.Find(LampName)?.GetComponent<Renderer>();
            _currentAngle = _fullAngle;
        }

        private void Update()
        {
            var tank = ResolveTank();
            if (tank == null) return;

            float target = NeedleAngle(tank.Level, tank.Capacity, _emptyAngle, _fullAngle);
            _currentAngle = Mathf.Lerp(_currentAngle, target, Mathf.Clamp01(_needleSmoothing * Time.deltaTime));
            if (_needle != null) _needle.localRotation = NeedleRotation(_neutralRotation, _needleAxis, _currentAngle);
            if (_lamp != null) _lamp.material.color = tank.IsEmpty ? _lampEmptyColor : _lampOkColor;
        }

        /// <summary>Pure mapping of level to needle angle; kept static so it can be unit tested without a scene.</summary>
        public static float NeedleAngle(float level, float capacity, float emptyAngle, float fullAngle)
        {
            float fraction = capacity > 0f ? Mathf.Clamp01(level / capacity) : 0f;
            return Mathf.Lerp(emptyAngle, fullAngle, fraction);
        }

        /// <summary>Pure composition of the imported neutral pose with a swing around the local axle; unit tested without a scene.</summary>
        public static Quaternion NeedleRotation(Quaternion neutralRotation, Vector3 axis, float angle) =>
            neutralRotation * Quaternion.AngleAxis(angle, axis);

        /// <summary>Depth-first search for the first descendant carrying one of the known indicator names.</summary>
        public static Transform? FindNeedle(Transform root)
        {
            foreach (var name in NeedleNames)
            {
                var found = FindDescendant(root, name);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform? FindDescendant(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var nested = FindDescendant(child, name);
                if (nested != null) return nested;
            }

            return null;
        }

        private IFuelTank? ResolveTank()
        {
            if (FuelTankLocator.IsAlive(_tank)) return _tank;

            var airship = GetComponentInParent<IAirshipView>();
            _tank = airship != null ? FuelTankLocator.Find(airship) : GetComponentInParent<IFuelTank>();
            return _tank;
        }
    }
}
