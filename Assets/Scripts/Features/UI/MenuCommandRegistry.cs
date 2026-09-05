#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.UI
{
    public interface IMenuCommandRegistry
    {
        bool TryGetCommand(string commandId, out IMenuCommand command);
    }

    /// <summary>
    /// Collects every registered <see cref="IMenuCommand"/> (same discovery style as InteractionHandlerRegistry).
    /// </summary>
    public class MenuCommandRegistry : IMenuCommandRegistry
    {
        private readonly Dictionary<string, IMenuCommand> _commandsById = new();

        public MenuCommandRegistry(IEnumerable<IMenuCommand> commands)
        {
            foreach (var command in commands)
            {
                if (string.IsNullOrEmpty(command.CommandId)) continue;
                _commandsById[command.CommandId] = command;
            }
        }

        public bool TryGetCommand(string commandId, out IMenuCommand command)
        {
            command = null!;
            return !string.IsNullOrEmpty(commandId) && _commandsById.TryGetValue(commandId, out command);
        }
    }
}
