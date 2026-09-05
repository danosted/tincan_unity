#nullable enable
using TinCan.Features.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace TinCan.UI
{
    /// <summary>
    /// Throwaway presentation for <see cref="IHudValues"/>: one label per value, top-left.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudOverlayView : MonoBehaviour
    {
        private IHudValues? _hud;
        private UIDocument? _document;
        private VisualElement? _panel;

        [Inject]
        public void Construct(IHudValues hud)
        {
            _hud = hud;
            _hud.Changed += Render;
            Render();
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            Render();
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.Changed -= Render;
        }

        private void Render()
        {
            if (_document == null || _hud == null) return;

            var root = _document.rootVisualElement;
            if (root == null) return;

            if (_panel == null)
            {
                _panel = new VisualElement();
                _panel.style.position = Position.Absolute;
                _panel.style.left = 12;
                _panel.style.top = 12;
                _panel.style.color = Color.white;
                _panel.pickingMode = PickingMode.Ignore;
                root.Add(_panel);
            }

            _panel.Clear();
            foreach (var pair in _hud.All)
            {
                _panel.Add(new Label($"{pair.Key}: {pair.Value}") { style = { fontSize = 20 } });
            }
        }
    }
}
