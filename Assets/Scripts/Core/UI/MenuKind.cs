#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// What kind of menu a request is. A later value beats an earlier one (<see cref="MenuArbitrationProcessor"/>), and
    /// the kind decides what shows behind it (<see cref="MenuBackdropRules"/>).
    /// </summary>
    public enum MenuKind
    {
        /// <summary>A menu beside the game: the HUD stays, the scene is not dimmed.</summary>
        Panel,
        /// <summary>A screen the player opened (the main or pause menu, a chooser): dims the scene, hides the HUD.</summary>
        Screen,
        /// <summary>A choice the game asks for (the end screen, the overboard choice): dims the scene, keeps the HUD.</summary>
        Modal
    }
}
