#nullable enable

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// A target whose HUD prompt says more than its interaction's <see cref="InteractionDefinition.Prompt"/>, such as how
    /// much a store holds ("Crystal bin 4/12"). Read on every peer, so it is built from replicated state.
    /// </summary>
    public interface IInteractionPrompt
    {
        string Prompt { get; }
    }
}
