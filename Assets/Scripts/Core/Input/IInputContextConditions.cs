#nullable enable
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Input
{
    /// <summary>Answers whether a context's <see cref="InputContext.Activation"/> holds right now, on this peer.</summary>
    public interface IInputContextConditions
    {
        bool Holds(InputContext context);
    }
}
