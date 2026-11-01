#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.UI;
using TinCan.Core.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using TinCan.Core.Domain.Hud;

namespace TinCan.App.Views
{
    /// <summary>
    /// Throwaway presentation for <see cref="IHudValues"/>: a <see cref="HudCard"/> on the Hud layer with a
    /// <see cref="Meter"/> per meter, then one line per value. Hidden while the menu on top says so
    /// (<see cref="MenuBackdropRules"/>) and while the HUD is empty.
    /// </summary>
    public class HudOverlayView : MonoBehaviour, IInjectedView
    {
        private IHudValues? _hud;
        private IMenuSystem? _menus;
        private IUiLayers? _layers;
        private HudCard? _card;

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
            _card?.RemoveFromHierarchy();
        }

        private void Render()
        {
            if (_layers == null || _hud == null || _menus == null) return;

            if (_card == null)
            {
                _card = new HudCard { name = "hud" };
                _layers.Root(UiLayer.Hud).Add(_card);
            }

            bool shown = MenuBackdropRules.For(_menus.Current?.Kind).ShowHud && _hud.Meters.Count + _hud.All.Count > 0;
            _card.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            _card.Clear();
            foreach (var meter in _hud.Meters) _card.Add(new Meter(meter.Key, meter.Value));
            foreach (var pair in _hud.All)
            {
                // A value-less line (a cue toast such as "Part repaired") shows the key alone.
                _card.AddLine(string.IsNullOrEmpty(pair.Value) ? pair.Key : $"{pair.Key}: {pair.Value}");
            }
        }
    }
}
