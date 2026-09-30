#nullable enable
using System;

namespace TinCan.Core.Domain.Input
{
    /// <summary>One row of a context's route table: when <see cref="Action"/> is pressed, run <see cref="Command"/>.</summary>
    [Serializable]
    public struct InputRoute
    {
        public InputActionId? Action;
        public InputCommand? Command;

        public InputRoute(InputActionId action, InputCommand command)
        {
            Action = action;
            Command = command;
        }
    }
}
