#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// What the shipyard's panels show: the mode, the parts to pick from (parts mode), and the controls for the mode.
    /// <see cref="Signature"/> changes whenever anything shown changes, so a view redraws only then.
    /// </summary>
    public sealed class ShipyardPanelModel
    {
        public ShipyardPanelModel(ShipyardMode mode, IReadOnlyList<ShipyardPartTile> parts, IReadOnlyList<ShipyardControlRow> controls)
        {
            Mode = mode;
            Parts = parts;
            Controls = controls;
            Signature = $"{mode}|{string.Join(",", parts.Select(p => p.Selected ? "*" + p.PartId : p.PartId))}|{string.Join(";", controls)}";
        }

        public ShipyardMode Mode { get; }
        public IReadOnlyList<ShipyardPartTile> Parts { get; }
        public IReadOnlyList<ShipyardControlRow> Controls { get; }
        public string Signature { get; }
    }
}
