using UnityEngine;
using TinCan.Features.Abilities;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Feature Layer: Provides visual feedback for the placement ghost.
    /// Swaps materials based on whether the current placement is valid.
    /// </summary>
    public class GhostMaterialFeedback : MonoBehaviour
    {
        [Header("Ghost Materials")]
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;

        private Renderer[] _renderers;
        private bool _lastIsValid;
        private bool _isInitialized;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _isInitialized = true;
            // Force initial update
            UpdateMaterials(false);
        }

        private void OnEnable()
        {
            _lastIsValid = false;
            UpdateMaterials(false);
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // Only run logic if we are named as a Ghost (to avoid the real module swapping materials)
            if (!name.EndsWith("_Ghost")) return;

            // Access the placement state via the bridge
            bool isValid = GASVisualScriptingBridge.IsPlacementValid();

            if (isValid != _lastIsValid)
            {
                UpdateMaterials(isValid);
                _lastIsValid = isValid;
            }
        }

        private void UpdateMaterials(bool isValid)
        {
            Material targetMat = isValid ? _validMaterial : _invalidMaterial;
            if (targetMat == null) return;

            foreach (var renderer in _renderers)
            {
                renderer.material = targetMat;
            }
        }
    }
}
