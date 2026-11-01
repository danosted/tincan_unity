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
    /// Throwaway presentation for <see cref="IHudValues"/>, top-left on a card (theme classes tc-hud, tc-meter): a
    /// labelled bar per meter, then one label per value, on the Hud layer. Hidden while the menu on top says so
    /// (<see cref="MenuBackdropRules"/>).
    /// </summary>
    public class HudOverlayView : MonoBehaviour, IInjectedView
    {
        private IHudValues? _hud;
        private IMenuSystem? _menus;
        private IUiLayers? _layers;
        private VisualElement? _panel;

        [Inject]
        public void Construct(IHudValues hud, IMenuSystem menus, IUiLayers layers)
        {
            _hud = hud;
            _menus = menus;
            _layers = layers;
            _hud.Changed += Render;
            _menus.Changed += Render;
            Render();
        }

        private void OnEnable()
        {
            Render();
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.Changed -= Render;
            if (_menus != null) _menus.Changed -= Render;
            _panel?.RemoveFromHierarchy();
        }

        private void Render()
        {
            if (_layers == null || _hud == null || _menus == null) return;

            if (_panel == null)
            {
                // A card behind the readouts keeps them legible over bright sky and deck (theme: .tc-hud).
                _panel = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
                _panel.AddToClassList("tc-hud");
                _layers.Root(UiLayer.Hud).Add(_panel);
            }

            // An empty HUD shows no card at all.
            bool shown = MenuBackdropRules.For(_menus.Current?.Kind).ShowHud && _hud.Meters.Count + _hud.All.Count > 0;
            _panel.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            _panel.Clear();
            foreach (var meter in _hud.Meters) _panel.Add(Meter(meter.Key, meter.Value));
            foreach (var pair in _hud.All)
            {
                // A value-less line (a cue toast such as "Part repaired") shows the key alone.
                string text = string.IsNullOrEmpty(pair.Value) ? pair.Key : $"{pair.Key}: {pair.Value}";
                var line = new Label(text) { pickingMode = PickingMode.Ignore };
                line.AddToClassList("tc-hud__line");
                _panel.Add(line);
            }
        }

        /// <summary>The theme class for a meter's fill: good (no modifier), fair below 60 %, bad below 25 %.</summary>
        public static string? MeterBand(float fill) => fill switch
        {
            < 0.25f => "tc-meter__fill--bad",
            < 0.6f => "tc-meter__fill--fair",
            _ => null
        };

        // A labelled bar; the width is the value, the colour the theme's band for it.
        private static VisualElement Meter(string label, float fill)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("tc-meter");
            var name = new Label(label) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("tc-meter__label");
            row.Add(name);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("tc-meter__track");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("tc-meter__fill");
            if (MeterBand(fill) is { } band) bar.AddToClassList(band);
            bar.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f);
            track.Add(bar);
            row.Add(track);
            return row;
        }
    }
}
