#nullable enable
using System;
using System.Linq;
using TinCan.Core.Ship.Parts;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard's panels, code-built UI Toolkit on its own document: on the right, the mode tabs and (parts mode) the
    /// parts with their pictures, grouped by category; below them the controls, keys on the left and what they do on the
    /// right. Redrawn only when the model's signature changes. Clicks on a tile or a tab are raised as events; the build
    /// ignores clicks over the panels (<see cref="IsPointerOver"/>).
    /// </summary>
    public sealed class ShipyardPanelView : IDisposable
    {
        private static readonly Color Card = new(0.08f, 0.09f, 0.11f, 0.86f);
        private static readonly Color Accent = new(0.3f, 0.8f, 1f, 1f);
        private static readonly Color Muted = new(0.75f, 0.77f, 0.8f, 1f);

        private readonly GameObject _host;
        private readonly UIDocument _document;
        private readonly Func<string, ShipPartDefinition?> _part;
        private readonly ShipyardThumbnails _thumbnails;
        private string? _signature;

        public ShipyardPanelView(Transform parent, PanelSettings settings, Func<string, ShipPartDefinition?> part, ShipyardThumbnails thumbnails)
        {
            _part = part;
            _thumbnails = thumbnails;
            _host = new GameObject("ShipyardPanels");
            _host.transform.SetParent(parent, false);
            _document = _host.AddComponent<UIDocument>();
            _document.panelSettings = settings;
            _document.sortingOrder = 10;
        }

        public event Action<string>? PartPicked;
        public event Action<ShipyardMode>? ModePicked;

        public void Show(ShipyardPanelModel model)
        {
            var root = _document.rootVisualElement;
            if (root == null || model.Signature == _signature) return;
            _signature = model.Signature;

            root.Clear();
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.top = root.style.right = root.style.bottom = 0;

            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute;
            column.style.right = 12;
            column.style.top = 12;
            column.style.bottom = 12;
            column.style.width = 300;
            column.style.flexDirection = FlexDirection.Column;
            column.style.justifyContent = Justify.SpaceBetween;
            root.Add(column);

            column.Add(BuildPanel(model));
            column.Add(ControlsTable(model));
        }

        public bool IsPointerOver(Vector2 screenPoint)
        {
            var panel = _document.rootVisualElement?.panel;
            if (panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            return panel.Pick(point) != null;
        }

        public void Dispose()
        {
            if (_host != null) Object.Destroy(_host);
        }

        private VisualElement BuildPanel(ShipyardPanelModel model)
        {
            var card = CardElement();
            card.style.flexShrink = 1;
            card.style.minHeight = 0;

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginBottom = 6;
            tabs.Add(Tab("Parts", model.Mode == ShipyardMode.Parts, () => ModePicked?.Invoke(ShipyardMode.Parts)));
            tabs.Add(Tab("Frame", model.Mode == ShipyardMode.Frame, () => ModePicked?.Invoke(ShipyardMode.Frame)));
            card.Add(tabs);

            if (model.Mode == ShipyardMode.Frame)
            {
                var hint = new Label("Draw the hull: click the floor to place nodes, click nodes to join them. A closed loop becomes a planked face.");
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.color = Muted;
                card.Add(hint);
                return card;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            foreach (var group in model.Parts.GroupBy(p => p.Category))
            {
                var heading = new Label(string.IsNullOrEmpty(group.Key) ? "Other" : group.Key);
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.color = Muted;
                heading.style.marginTop = 4;
                scroll.Add(heading);

                var grid = new VisualElement();
                grid.style.flexDirection = FlexDirection.Row;
                grid.style.flexWrap = Wrap.Wrap;
                foreach (var tile in group) grid.Add(Tile(tile));
                scroll.Add(grid);
            }

            card.Add(scroll);
            return card;
        }

        private VisualElement Tile(ShipyardPartTile tile)
        {
            var button = new Button(() => PartPicked?.Invoke(tile.PartId)) { tooltip = tile.PartId };
            button.style.width = 84;
            button.style.height = 104;
            button.style.marginLeft = button.style.marginRight = 2;
            button.style.marginTop = button.style.marginBottom = 2;
            button.style.paddingLeft = button.style.paddingRight = button.style.paddingTop = button.style.paddingBottom = 2;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 1f);
            var border = tile.Selected ? Accent : new Color(0f, 0f, 0f, 0f);
            button.style.borderLeftColor = button.style.borderRightColor = button.style.borderTopColor = button.style.borderBottomColor = border;
            button.style.borderLeftWidth = button.style.borderRightWidth = button.style.borderTopWidth = button.style.borderBottomWidth = 2;

            var picture = new VisualElement();
            picture.style.width = 76;
            picture.style.height = 76;
            if (_part(tile.PartId) is { } part && _thumbnails.Of(part) is { } texture)
            {
                picture.style.backgroundImage = Background.FromRenderTexture((RenderTexture)texture);
            }

            button.Add(picture);
            var name = new Label(tile.Name);
            name.style.fontSize = 11;
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.color = tile.Selected ? Accent : Color.white;
            button.Add(name);
            return button;
        }

        private static VisualElement ControlsTable(ShipyardPanelModel model)
        {
            var card = CardElement();
            card.style.marginTop = 8;
            card.style.flexShrink = 0;
            foreach (var row in model.Controls)
            {
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.marginBottom = 1;

                var keys = new Label(row.Keys);
                keys.style.width = 120;
                keys.style.unityFontStyleAndWeight = FontStyle.Bold;
                keys.style.color = Accent;
                keys.style.whiteSpace = WhiteSpace.Normal;
                line.Add(keys);

                var action = new Label(row.Action);
                action.style.flexGrow = 1;
                action.style.flexShrink = 1;
                action.style.whiteSpace = WhiteSpace.Normal;
                action.style.color = Color.white;
                line.Add(action);
                card.Add(line);
            }

            return card;
        }

        private static Button Tab(string text, bool active, Action onClick)
        {
            var tab = new Button(onClick) { text = text };
            tab.style.flexGrow = 1;
            tab.style.height = 26;
            tab.style.backgroundColor = active ? new Color(0.18f, 0.35f, 0.45f, 1f) : new Color(0.14f, 0.15f, 0.18f, 1f);
            tab.style.color = active ? Color.white : Muted;
            tab.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            return tab;
        }

        private static VisualElement CardElement()
        {
            var card = new VisualElement();
            card.style.backgroundColor = Card;
            card.style.paddingLeft = card.style.paddingRight = 10;
            card.style.paddingTop = card.style.paddingBottom = 8;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            card.style.flexDirection = FlexDirection.Column;
            return card;
        }
    }
}
