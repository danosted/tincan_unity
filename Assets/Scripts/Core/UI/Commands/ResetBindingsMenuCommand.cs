#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.Core.UI.Commands
{
    /// <summary>Controls menu: every key back to its default.</summary>
    public class ResetBindingsMenuCommand : IMenuCommand
    {
        public const string Id = "ResetBindings";

        private readonly IInputBindings _bindings;

        public ResetBindingsMenuCommand(IInputBindings bindings)
        {
            _bindings = bindings;
        }

        public string CommandId => Id;

        public void Execute(MenuContext context) => _bindings.ResetAll();
    }
}
