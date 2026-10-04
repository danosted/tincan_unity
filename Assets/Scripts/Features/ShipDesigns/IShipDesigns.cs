#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Which design ships are built from. The server gives every ship without a design the selected one: the design
    /// handed over by <see cref="Select"/> (the shipyard's Launch), else the stored design named by the
    /// <c>-shipDesign &lt;name&gt;</c> launch argument, else <see cref="ShipDesignsConfig.DefaultDesign"/>.
    /// </summary>
    public interface IShipDesigns
    {
        /// <summary>The design ships get from now on (ships already built keep theirs until <see cref="TryApply"/>).</summary>
        void Select(ShipDesign design);

        /// <summary>Server: validates the design and gives it to the ship's state; false with the reason if refused.</summary>
        bool TryApply(IShipDesignState state, ShipDesign design, out string? error);
    }
}
