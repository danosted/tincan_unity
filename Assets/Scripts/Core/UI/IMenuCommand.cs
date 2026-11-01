#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// A headless menu action. Register implementations with <c>.As&lt;IMenuCommand&gt;()</c>;
    /// <see cref="MenuCommandRegistry"/> collects them and <see cref="MenuUseCase"/> dispatches by CommandId.
    /// </summary>
    public interface IMenuCommand
    {
        string CommandId { get; }
        void Execute(MenuContext context);
    }
}
