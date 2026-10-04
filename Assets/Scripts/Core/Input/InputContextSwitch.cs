#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Input
{
    /// <summary>The <see cref="InputContextActivation.WhileOpened"/> contexts their owners have opened.</summary>
    public sealed class InputContextSwitch : IInputContextSwitch
    {
        private readonly HashSet<InputContext> _open = new();

        public void SetOpen(InputContext context, bool open)
        {
            if (open) _open.Add(context);
            else _open.Remove(context);
        }

        public bool IsOpen(InputContext context) => _open.Contains(context);
    }
}
