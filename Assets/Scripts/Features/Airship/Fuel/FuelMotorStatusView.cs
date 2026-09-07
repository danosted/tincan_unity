#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>Colors the receiver's rune dial at low fuel and extinguishes its inner wind at zero.</summary>
    public class FuelMotorStatusView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Show the low-energy warning at or below this fraction of tank capacity. Does not change engine stalling.")]
        [Range(0f, 1f)]
        [SerializeField] private float _warningFraction = 0.05f;
        [Tooltip("Rune dial color while energy is above the warning threshold.")]
        [SerializeField] private Color _fuelAvailableColor = new(0.4f, 0.87f, 1f);
        [Tooltip("Rune dial color at or below the warning threshold.")]
        [SerializeField] private Color _warningColor = new(1f, 0.1f, 0.05f);

        private readonly List<Renderer> _runes = new();
        private readonly List<GameObject> _heartWinds = new();
        private MaterialPropertyBlock? _properties;
        private bool? _lastWarning;
        private bool? _lastPowered;

        private void CacheVisuals()
        {
            _runes.Clear();
            _heartWinds.Clear();
            foreach (var node in GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "FuelMotor_DialCore" || node.name.StartsWith("FuelMotor_DialRune"))
                {
                    var renderer = node.GetComponent<Renderer>();
                    if (renderer != null) _runes.Add(renderer);
                }
                if (node.name == "FuelMotor_Heart_Wind_1" || node.name == "FuelMotor_Heart_Wind_2")
                    _heartWinds.Add(node.gameObject);
            }
            _properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _lastWarning = null;
            _lastPowered = null;
            RefreshVisuals();
        }

        private void Update() => RefreshVisuals();

        /// <summary>Read the current tank on every refresh, including after fixture reparenting.</summary>
        public void RefreshVisuals()
        {
            if (_properties == null) CacheVisuals();
            var tank = GetComponentInParent<IFuelTank>(true);
            if (tank == null) return;

            bool powered = tank.Level > 0f && tank.Capacity > 0f;
            if (_lastPowered != powered)
            {
                _lastPowered = powered;
                foreach (var wind in _heartWinds) if (wind != null) wind.SetActive(powered);
            }

            bool warning = IsWarning(tank.Level, tank.Capacity, _warningFraction);
            if (_lastWarning == warning || _properties == null) return;
            _lastWarning = warning;
            Color color = warning ? _warningColor : _fuelAvailableColor;
            foreach (var rune in _runes)
            {
                if (rune == null) continue;
                rune.GetPropertyBlock(_properties);
                _properties.SetColor(BaseColorId, color);
                _properties.SetColor(EmissionColorId, color);
                rune.SetPropertyBlock(_properties);
            }
        }

        public static bool IsWarning(float level, float capacity, float warningFraction = 0.05f) =>
            capacity <= 0f || level <= capacity * Mathf.Clamp01(warningFraction);
    }
}
