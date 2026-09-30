#nullable enable
namespace TinCan.Core.Input
{
    /// <summary>Set while the Controls menu waits for a key; activates the Rebinding context, which silences everything else.</summary>
    public sealed class InputRebindState
    {
        public bool IsRebinding { get; set; }
    }
}
