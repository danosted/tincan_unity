#nullable enable
using System.Collections.Generic;

namespace TinCan.Core.Domain.Abilities.Tags
{
    /// <summary>
    /// Every <see cref="GameplayTag"/> asset the game knows, looked up by name. Tags cross the network as their names
    /// (a ScriptableObject reference cannot), so the receiving side resolves the name back to the asset here.
    /// </summary>
    public interface IGameplayTagRegistry
    {
        IReadOnlyCollection<GameplayTag> All { get; }
        bool TryGet(string name, out GameplayTag tag);
    }
}
