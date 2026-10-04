#nullable enable
namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// Opens and closes <see cref="InputContextActivation.WhileOpened"/> contexts: for a mode the player enters and leaves
    /// that no possession, tag or menu describes (the shipyard). Only the system that owns the context opens it.
    /// </summary>
    public interface IInputContextSwitch
    {
        void SetOpen(InputContext context, bool open);

        bool IsOpen(InputContext context);
    }
}
