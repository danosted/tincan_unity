#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using TinCan.Core.Domain.Hud;

namespace TinCan.UI
{
    /// <summary>
    /// Throwaway presentation for <see cref="IHudValues"/>: one label per value, top-left.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudOverlayView : MonoBehaviour, IInjectedView
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
                // A value-less line (a cue toast such as "Part repaired") shows the key alone.
                string text = string.IsNullOrEmpty(pair.Value) ? pair.Key : $"{pair.Key}: {pair.Value}";
                _panel.Add(new Label(text) { style = { fontSize = 20 } });
            }
        }
    }
}
