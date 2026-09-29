#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using TinCan.Core.Domain.Hud;

namespace TinCan.App.Views
{
    /// <summary>
    /// Throwaway presentation for <see cref="IHudValues"/>, top-left: a labelled bar per meter, then one label per value.
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
            foreach (var meter in _hud.Meters) _panel.Add(Meter(meter.Key, meter.Value));
            foreach (var pair in _hud.All)
            {
                // A value-less line (a cue toast such as "Part repaired") shows the key alone.
                string text = string.IsNullOrEmpty(pair.Value) ? pair.Key : $"{pair.Key}: {pair.Value}";
                _panel.Add(new Label(text) { style = { fontSize = 20 } });
            }
        }

        // A labelled bar; the fill runs red (empty) to green (full).
        private static VisualElement Meter(string label, float fill)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            row.Add(new Label(label) { style = { fontSize = 20, width = 80 } });

            var track = new VisualElement
            {
                style =
                {
                    width = 240, height = 16,
                    backgroundColor = new Color(0f, 0f, 0f, 0.55f),
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = Color.white, borderBottomColor = Color.white, borderLeftColor = Color.white, borderRightColor = Color.white
                }
            };
            track.Add(new VisualElement
            {
                style =
                {
                    width = Length.Percent(fill * 100f), height = Length.Percent(100f),
                    backgroundColor = Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.3f, 0.85f, 0.35f), fill)
                }
            });
            row.Add(track);
            return row;
        }
    }
}
