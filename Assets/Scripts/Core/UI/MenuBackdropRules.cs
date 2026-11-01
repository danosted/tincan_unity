#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// The rule table for what sits behind a menu. A Screen is the player's own (main, pause, a chooser): the scene dims
    /// and the HUD goes. A Modal is the game asking (overboard, the end screen): the scene dims but the HUD stays, since
    /// its lines (the respawn countdown, the voyage) belong with the choice. A Panel sits beside the game and changes
    /// nothing. The views read this; none of them decides it.
    /// </summary>
    public static class MenuBackdropRules
    {
        public static MenuBackdrop For(MenuKind? top) => top switch
        {
            MenuKind.Screen => new MenuBackdrop(dimScene: true, showHud: false),
            MenuKind.Modal => new MenuBackdrop(dimScene: true, showHud: true),
            _ => new MenuBackdrop(dimScene: false, showHud: true)
        };
    }
}
