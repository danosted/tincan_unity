#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// The screen's layers, back to front. The order of these values is the draw order (<see cref="UiLayers"/> adds one
    /// container per value, in this order), so it is the one place a layer's sort order is defined.
    /// </summary>
    public enum UiLayer
    {
        /// <summary>Indicators that follow something in the world (pings, markers).</summary>
        World,
        Hud,
        /// <summary>Feature panels that sit beside the game, such as the shipyard's parts panel.</summary>
        Panels,
        Menus,
        /// <summary>Choices the game asks for: the voyage end screen, the overboard choice, connection messages.</summary>
        Modal,
        Toasts,
        /// <summary>Developer readouts (the net harness overlay).</summary>
        Dev
    }
}
