#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Infrastructure.Abilities
{
    /// <summary>
    /// Concrete implementation of the Ability Registry.
    /// Maintained by the ActorOrchestrator.
    /// </summary>
    public class AbilityRegistry : IAbilityRegistry
    {
        private readonly List<IAbilityControllerBase> _controllers = new();

        public IEnumerable<IAbilityControllerBase> AllControllers => _controllers;

        public void Register(IAbilityControllerBase controller)
        {
            if (!_controllers.Contains(controller))
            {
                _controllers.Add(controller);
            }
        }

        public void Unregister(IAbilityControllerBase controller)
        {
            _controllers.Remove(controller);
        }
    }
}
