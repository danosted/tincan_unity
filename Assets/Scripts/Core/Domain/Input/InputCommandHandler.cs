#nullable enable
using System;

namespace TinCan.Core.Domain.Input
{
    /// <summary>Typed base for <see cref="IInputCommandHandler"/>: one handler per command type.</summary>
    public abstract class InputCommandHandler<TCommand> : IInputCommandHandler where TCommand : InputCommand
    {
        public Type CommandType => typeof(TCommand);

        public bool TryHandle(InputCommand command) => command is TCommand typed && Handle(typed);

        /// <summary>Return false when the command does not apply right now, so a lower context may take the action.</summary>
        protected abstract bool Handle(TCommand command);
    }
}
