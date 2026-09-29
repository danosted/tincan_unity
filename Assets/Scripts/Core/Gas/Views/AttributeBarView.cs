#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TinCan.Core.Gas.Views
{
    /// <summary>
    /// Presentation Layer: a world-space bar showing one GAS attribute of the nearest ability controller above it, as a
    /// fraction of a max attribute (or a fixed max). Reusable for anything with attributes: ship part health while it
    /// is repaired, and later stamina, shields or a reload. Attributes replicate, so every peer and late joiner sees
    /// the same bar. Drop <c>Assets/Prefabs/UI/AttributeBar.prefab</c> under the actor and pick the attributes; the
    /// bar's parts are found by name ("Fill"), and it turns to face the local view camera.
    /// </summary>
    public sealed class AttributeBarView : MonoBehaviour
    {
        private const string FillName = "Fill";

        [Tooltip("The value shown (for example Health).")]
        [SerializeField] private GameplayAttribute? _attribute;
        [Tooltip("What a full bar means (for example MaxHealth). Empty uses Max Value.")]
        [SerializeField] private GameplayAttribute? _maxAttribute;
        [Min(0.0001f)] [SerializeField] private float _maxValue = 100f;
        [Tooltip("Off: the bar only shows while the value is below its max (a damaged part, a draining meter).")]
        [SerializeField] private bool _showWhenFull;
        [SerializeField] private Color _emptyColor = new(0.85f, 0.2f, 0.15f);
        [SerializeField] private Color _fullColor = new(0.3f, 0.85f, 0.35f);

        private IAbilityControllerBase? _controller;
        private ILocalViewCamera? _viewCamera;
        private Canvas? _canvas;
        private RectTransform? _fill;
        private Graphic? _fillGraphic;

        [Inject]
        public void Construct(ILocalViewCamera viewCamera) => _viewCamera = viewCamera;

        /// <summary>Whether the bar shows, and how full it is (0..1), for a value against its max.</summary>
        public static (bool Visible, float Fill) Evaluate(float current, float max, bool showWhenFull)
        {
            float fill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            return (showWhenFull || fill < 1f, fill);
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _fill = transform.FindDescendant(FillName) as RectTransform;
            _fillGraphic = _fill != null ? _fill.GetComponent<Graphic>() : null;
            _controller = GetComponentInParent<IAbilityControllerBase>();
        }

        private void LateUpdate()
        {
            if (_controller == null || _attribute == null) { SetVisible(false); return; }

            float current = _controller.TryGetAttribute(_attribute, out var value) ? value.CurrentValue : 0f;
            float max = _maxAttribute != null && _controller.TryGetAttribute(_maxAttribute, out var maxValue) ? maxValue.CurrentValue : _maxValue;
            var (visible, fill) = Evaluate(current, max, _showWhenFull);

            SetVisible(visible);
            if (!visible) return;

            if (_fill != null) _fill.anchorMax = new Vector2(fill, _fill.anchorMax.y);
            if (_fillGraphic != null) _fillGraphic.color = Color.Lerp(_emptyColor, _fullColor, fill);

            var camera = _viewCamera?.Camera;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.enabled != visible) _canvas.enabled = visible;
        }
    }
}
