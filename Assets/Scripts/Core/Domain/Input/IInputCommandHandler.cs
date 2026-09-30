#nullable enable
using System;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// Carries out one <see cref="InputCommand"/> type when a context routes an action to it. Register with
    /// <c>.As&lt;IInputCommandHandler&gt;()</c> in the owning system's installer; the router finds it by
    /// <see cref="CommandType"/>. Derive from <see cref="InputCommandHandler{TCommand}"/>.
    /// </summary>
    public interface IInputCommandHandler
    {
        Type CommandType { get; }

        /// <summary>True when the command did something: the action is then consumed and lower contexts never see it.</summary>
        bool TryHandle(InputCommand command);
    }
}
