#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Domain.Abilities
{
    /// <summary>
    /// Registry for all actors that support the Gameplay Ability System.
    /// This allows the Ability System to process ticks and events independently of movement or generic actor updates.
    /// </summary>
    public interface IAbilityRegistry
    {
        IEnumerable<IAbilityControllerBase> AllControllers { get; }

        void Register(IAbilityControllerBase controller);
        void Unregister(IAbilityControllerBase controller);
    }
}
