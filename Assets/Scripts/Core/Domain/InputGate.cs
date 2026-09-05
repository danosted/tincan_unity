#nullable enable
namespace TinCan.Core.Domain
{
    /// <summary>
    /// Blocks gameplay input while a menu owns the screen. Cancel always passes so the menu can be closed again.
    /// Set by the menu bootstrap, read by the input service.
    /// </summary>
    public sealed class InputGate
    {
        public bool GameplayBlocked { get; set; }

        public bool Allows(string actionName) => !GameplayBlocked || actionName == ActionNames.Cancel;
    }
}
