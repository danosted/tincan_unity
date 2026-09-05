#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Presentation Layer: an in-world dial at the helm. Polls the ship's fuel tank every frame (like AirshipDoor),
    /// so late joiners and clients need no extra sync; rotates the needle child between the empty and full angles
    /// and tints the optional lamp when the tank is dry. Sits anywhere under the airship prefab.
    /// </summary>
    public class FuelGaugeView : MonoBehaviour
    {
        private const string NeedleName = "Needle";
        private const string LampName = "Lamp";

        [SerializeField] private float _emptyAngle = 120f;
        [SerializeField] private float _fullAngle = -120f;
        [SerializeField] private float _needleSmoothing = 6f;
        [SerializeField] private Color _lampOkColor = new(0.1f, 0.6f, 0.1f);
        [SerializeField] private Color _lampEmptyColor = new(0.9f, 0.1f, 0.1f);

        private Transform? _needle;
        private Renderer? _lamp;
        private IFuelTank? _tank;
        private float _currentAngle;

        private void Awake()
        {
            _needle = transform.Find(NeedleName);
            _lamp = transform.Find(LampName)?.GetComponent<Renderer>();
            _currentAngle = _fullAngle;
        }

        private void Update()
        {
            var tank = ResolveTank();
            if (tank == null) return;

            float target = NeedleAngle(tank.Level, tank.Capacity, _emptyAngle, _fullAngle);
            _currentAngle = Mathf.Lerp(_currentAngle, target, Mathf.Clamp01(_needleSmoothing * Time.deltaTime));
            if (_needle != null) _needle.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
            if (_lamp != null) _lamp.material.color = tank.IsEmpty ? _lampEmptyColor : _lampOkColor;
        }

        /// <summary>Pure mapping of level to needle angle; kept static so it can be unit tested without a scene.</summary>
        public static float NeedleAngle(float level, float capacity, float emptyAngle, float fullAngle)
        {
            float fraction = capacity > 0f ? Mathf.Clamp01(level / capacity) : 0f;
            return Mathf.Lerp(emptyAngle, fullAngle, fraction);
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
