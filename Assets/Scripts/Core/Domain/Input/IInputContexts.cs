#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Input
{
    /// <summary>Which contexts are live this frame, highest priority first. Read-only; the input system evaluates it.</summary>
    public interface IInputContexts
    {
        /// <summary>Active and not blocked, highest priority first.</summary>
        IReadOnlyList<InputContext> Active { get; }

        bool IsActive(InputContext? context);

        /// <summary>Raised when <see cref="Active"/> changes.</summary>
        event Action? Changed;
    }
}
