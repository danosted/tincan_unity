#nullable enable
using System;
using System.Linq;
using TinCan.Core.Ship.Parts;
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard's panels, code-built UI Toolkit on the Panels layer (<see cref="IUiLayers"/>): on the right, the mode
    /// tabs and (parts mode) the parts with their pictures, grouped by category; below them the controls, keys on the left
    /// and what they do on the right. Its look is the theme's (tc-card, tc-tab, tc-tile, tc-keys in
    /// Assets/UI/Theme/TinCanTheme.uss). Redrawn only when the model's signature changes. Clicks on a tile or a tab are
    /// raised as events; the build ignores clicks over the panels (<see cref="IsPointerOver"/>).
    /// </summary>
    public sealed class ShipyardPanelView : IDisposable
    {
        private readonly VisualElement _root;
        private readonly Func<string, ShipPartDefinition?> _part;
        private readonly ShipyardThumbnails _thumbnails;
        private string? _signature;

        public ShipyardPanelView(IUiLayers layers, Func<string, ShipPartDefinition?> part, ShipyardThumbnails thumbnails)
        {
            _part = part;
            _thumbnails = thumbnails;
            _root = Classed(new VisualElement { name = "shipyard-panels", pickingMode = PickingMode.Ignore }, "tc-fill");
            layers.Root(UiLayer.Panels).Add(_root);
        }

        public event Action<string>? PartPicked;
        public event Action<ShipyardMode>? ModePicked;

        public void Show(ShipyardPanelModel model)
        {
            if (model.Signature == _signature) return;
            _signature = model.Signature;

            _root.Clear();
            var column = Classed(new VisualElement { pickingMode = PickingMode.Ignore }, "tc-side-column");
            _root.Add(column);
            column.Add(BuildPanel(model));
            column.Add(ControlsTable(model));
        }

        /// <summary>Whether the topmost element under the point is one of these panels (the panel is shared with every layer).</summary>
        public bool IsPointerOver(Vector2 screenPoint)
        {
            var panel = _root.panel;
            if (panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            var picked = panel.Pick(point);
            return picked != null && _root.Contains(picked);
        }

        public void Dispose()
        {
            _root.RemoveFromHierarchy();
        }

        private VisualElement BuildPanel(ShipyardPanelModel model)
        {
            var card = Classed(new VisualElement(), "tc-card", "tc-card--fill");

            var tabs = Classed(new VisualElement(), "tc-tabs");
            tabs.Add(Tab("Parts", model.Mode == ShipyardMode.Parts, () => ModePicked?.Invoke(ShipyardMode.Parts)));
            tabs.Add(Tab("Frame", model.Mode == ShipyardMode.Frame, () => ModePicked?.Invoke(ShipyardMode.Frame)));
            card.Add(tabs);

            if (model.Mode == ShipyardMode.Frame)
            {
                card.Add(Classed(new Label("Draw the hull: click the floor to place nodes, click nodes to join them. A closed loop becomes a planked face."), "tc-hint"));
                return card;
            }

            var scroll = Classed(new ScrollView(ScrollViewMode.Vertical), "tc-menu__rows");
            foreach (var group in model.Parts.GroupBy(p => p.Category))
            {
                scroll.Add(Classed(new Label(string.IsNullOrEmpty(group.Key) ? "Other" : group.Key), "tc-heading"));

                var grid = Classed(new VisualElement(), "tc-grid");
                foreach (var tile in group) grid.Add(Tile(tile));
                scroll.Add(grid);
            }

            card.Add(scroll);
            return card;
        }

        private VisualElement Tile(ShipyardPartTile tile)
        {
            var button = Classed(new Button(() => PartPicked?.Invoke(tile.PartId)) { tooltip = tile.PartId }, "tc-tile");
            if (tile.Selected) button.AddToClassList("tc-tile--selected");

            var picture = Classed(new VisualElement(), "tc-tile__picture");
            if (_part(tile.PartId) is { } part && _thumbnails.Of(part) is { } texture)
            {
                picture.style.backgroundImage = Background.FromRenderTexture((RenderTexture)texture);
            }

            button.Add(picture);
            button.Add(Classed(new Label(tile.Name), "tc-tile__name"));
            return button;
        }

        private static VisualElement ControlsTable(ShipyardPanelModel model)
        {
            var card = Classed(new VisualElement(), "tc-card", "tc-card--below");
            foreach (var row in model.Controls)
            {
                var line = Classed(new VisualElement(), "tc-keys");
                line.Add(Classed(new Label(row.Keys), "tc-keys__key"));
                line.Add(Classed(new Label(row.Action), "tc-keys__action"));
                card.Add(line);
            }

            return card;
        }

        private static Button Tab(string text, bool active, Action onClick)
        {
            var tab = Classed(new Button(onClick) { text = text }, "tc-tab");
            if (active) tab.AddToClassList("tc-tab--active");
            return tab;
        }

        private static T Classed<T>(T element, params string[] classes) where T : VisualElement
        {
            foreach (var name in classes) element.AddToClassList(name);
            return element;
        }
    }
}
