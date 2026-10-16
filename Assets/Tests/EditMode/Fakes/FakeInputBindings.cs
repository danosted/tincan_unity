#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Input;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Bindings without an input system: no slots, and an action is described by its path ("Shipyard/Fly").</summary>
    public sealed class FakeInputBindings : IInputBindings
    {
        public IReadOnlyList<InputBindingSlot> Slots => Array.Empty<InputBindingSlot>();
        public InputBindingSlot? Rebinding => null;
        public string? LastMessage => null;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public string Describe(InputBindingSlot slot) => slot.Label;

        public string DescribeAction(InputActionId action) => action.Path;

        public void StartRebind(InputBindingSlot slot)
        {
        }

        public void CancelRebind()
        {
        }

        public void ResetAll()
        {
        }
    }
}
