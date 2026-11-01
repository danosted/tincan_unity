#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>What shows behind the menu on top (<see cref="MenuBackdropRules"/>).</summary>
    public readonly struct MenuBackdrop
    {
        public readonly bool DimScene;
        public readonly bool ShowHud;

        public MenuBackdrop(bool dimScene, bool showHud)
        {
            DimScene = dimScene;
            ShowHud = showHud;
        }
    }
}
