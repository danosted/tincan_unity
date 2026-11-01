#nullable enable
using System;
using System.Linq;
using TinCan.Core.Ship.Parts;
using TinCan.Core.UI;
using TinCan.Core.UI.Components;
using TinCan.Features.Shipyard.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard's panels on the Panels layer (<see cref="IUiLayers"/>), composed from UI components: down the right
    /// edge a <see cref="Card"/> with the mode <see cref="TabBar"/> and (parts mode) a <see cref="PartTile"/> per part,
    /// grouped by category; below it a <see cref="KeyHintTable"/> of the controls. Redrawn only when the model's
    /// signature changes. Clicks on a tile or a tab are raised as events; the build ignores clicks over the panels
    /// (<see cref="IsPointerOver"/>).
    /// </summary>
    public sealed class ShipyardPanelView : IDisposable
    {
        private const string FillClass = "tc-fill";
        private const string SideColumnClass = "tc-side-column";
        private const string GridClass = "tc-grid";

        private readonly VisualElement _root;
        private readonly Func<string, ShipPartDefinition?> _part;
        private readonly ShipyardThumbnails _thumbnails;
        private string? _signature;

        public ShipyardPanelView(IUiLayers layers, Func<string, ShipPartDefinition?> part, ShipyardThumbnails thumbnails)
        {
            _part = part;
            _thumbnails = thumbnails;
            _root = new VisualElement { name = "shipyard-panels", pickingMode = PickingMode.Ignore };
            _root.AddToClassList(FillClass);
            layers.Root(UiLayer.Panels).Add(_root);
        }

        public event Action<string>? PartPicked;
        public event Action<ShipyardMode>? ModePicked;

        public void Show(ShipyardPanelModel model)
        {
            if (model.Signature == _signature) return;
            _signature = model.Signature;

            _root.Clear();
            var column = new VisualElement { pickingMode = PickingMode.Ignore };
            column.AddToClassList(SideColumnClass);
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
            var card = new Card { Grow = true };

            var tabs = new TabBar();
            tabs.AddTab("Parts", model.Mode == ShipyardMode.Parts, () => ModePicked?.Invoke(ShipyardMode.Parts));
            tabs.AddTab("Frame", model.Mode == ShipyardMode.Frame, () => ModePicked?.Invoke(ShipyardMode.Frame));
            card.Add(tabs);

            if (model.Mode == ShipyardMode.Frame)
            {
                card.Add(Card.Hint("Draw the hull: click the floor to place nodes, click nodes to join them. A closed loop becomes a planked face."));
                return card;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(MenuPanel.RowsUssClassName);
            foreach (var group in model.Parts.GroupBy(p => p.Category))
            {
                scroll.Add(Card.Heading(string.IsNullOrEmpty(group.Key) ? "Other" : group.Key));

                var grid = new VisualElement();
                grid.AddToClassList(GridClass);
                foreach (var tile in group) grid.Add(Tile(tile));
                scroll.Add(grid);
            }

            card.Add(scroll);
            return card;
        }

        private PartTile Tile(ShipyardPartTile tile)
        {
            var view = new PartTile(tile.Name, () => PartPicked?.Invoke(tile.PartId)) { tooltip = tile.PartId, Selected = tile.Selected };
            if (_part(tile.PartId) is { } part && _thumbnails.Of(part) is RenderTexture texture) view.ShowPicture(texture);
            return view;
        }

        private static VisualElement ControlsTable(ShipyardPanelModel model)
        {
            var card = new Card { Below = true };
            var table = new KeyHintTable();
            foreach (var row in model.Controls) table.AddRow(row.Keys, row.Action);
            card.Add(table);
            return card;
        }
    }
}
