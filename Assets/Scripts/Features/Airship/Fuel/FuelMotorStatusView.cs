#nullable enable
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>Shows the motor's low-fuel lamp and EMPTY plate from the replicated tank level.</summary>
    public class FuelMotorStatusView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Show the EMPTY warning at or below this fraction of tank capacity. Does not change engine stalling.")]
        [Range(0f, 1f)]
        [SerializeField] private float _warningFraction = 0.05f;
        [Tooltip("Motor lamp color while fuel is above the warning threshold.")]
        [SerializeField] private Color _fuelAvailableColor = new(0.1f, 0.6f, 0.1f);
        [Tooltip("Motor lamp color at or below the warning threshold.")]
        [SerializeField] private Color _warningColor = new(0.9f, 0.1f, 0.1f);

        private GameObject? _emptyPlate;
        private GameObject? _emptyText;
        private Renderer? _lamp;
        private MaterialPropertyBlock? _lampProperties;
        private bool? _lastWarning;

        private void CacheVisuals()
        {
            foreach (var node in GetComponentsInChildren<Transform>(true))
            {
                switch (node.name)
                {
                    case "FuelMotor_EmptyPlate": _emptyPlate = node.gameObject; break;
                    case "FuelMotor_EmptyText": _emptyText = node.gameObject; break;
                    case "FuelStatusLamp": _lamp = node.GetComponent<Renderer>(); break;
                }
            }

            _lampProperties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _lastWarning = null;
            RefreshVisuals();
        }

        private void Update() => RefreshVisuals();

        /// <summary>Read the current tank on every refresh, including after fixture reparenting.</summary>
        public void RefreshVisuals()
        {
            if (_lampProperties == null) CacheVisuals();
            var tank = GetComponentInParent<IFuelTank>();
            if (tank == null) return;

            bool warning = IsWarning(tank.Level, tank.Capacity, _warningFraction);
            if (_lastWarning == warning) return;
            _lastWarning = warning;

            if (_emptyPlate != null) _emptyPlate.SetActive(warning);
            if (_emptyText != null) _emptyText.SetActive(warning);
            if (_lamp == null || _lampProperties == null) return;

            Color color = warning ? _warningColor : _fuelAvailableColor;
            _lamp.GetPropertyBlock(_lampProperties);
            _lampProperties.SetColor(BaseColorId, color);
            _lampProperties.SetColor(EmissionColorId, color);
            _lamp.SetPropertyBlock(_lampProperties);
        }

        public static bool IsWarning(float level, float capacity, float warningFraction = 0.05f) =>
            capacity <= 0f || level <= capacity * Mathf.Clamp01(warningFraction);
    }
}
