#nullable enable
namespace TinCan.Features.ShipLoss
{
    /// <summary>The Lost screen's ids (Assets/UI/Menus/Menu_ShipLost, built by TinCan > Dev > Ship Loss > Build Assets).</summary>
    public static class ShipLossMenus
    {
        public const string MenuId = "ship_lost";
        public const string RespawnItem = "respawn";

        /// <summary>
        /// The <see cref="TinCan.Core.UI.IMenuRowSource"/> whose rows the Lost screen shows between Respawn and Quit: a
        /// feature on top adds its own choices there (the voyage: Restart Voyage). With no such source there are none.
        /// </summary>
        public const string ChoicesSource = "ship-lost-choices";
    }
}
