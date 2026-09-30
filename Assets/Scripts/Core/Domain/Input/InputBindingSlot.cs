#nullable enable
namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// One key a player can change: a binding of a rebindable action (a composite's part counts on its own, so "Move"
    /// has Forward, Back, Left and Right). Listed by <see cref="IInputBindings.Slots"/> for the Controls menu.
    /// </summary>
    public readonly struct InputBindingSlot
    {
        public readonly InputActionId Action;
        public readonly int BindingIndex;
        /// <summary>What the row says: "Jump", "Move Forward".</summary>
        public readonly string Label;
        /// <summary>The situation it belongs to (the action's map): "Humanoid", "Airship".</summary>
        public readonly string Group;

        public InputBindingSlot(InputActionId action, int bindingIndex, string label, string group)
        {
            Action = action;
            BindingIndex = bindingIndex;
            Label = label;
            Group = group;
        }

        public bool SameAs(InputBindingSlot other) => ReferenceEquals(Action, other.Action) && BindingIndex == other.BindingIndex;
    }
}
