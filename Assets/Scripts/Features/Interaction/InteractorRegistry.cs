using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Infrastructure Layer: Concrete implementation of IInteractorRegistry.
    /// Tracks all active IInteractorView components.
    /// </summary>
    public class InteractorRegistry : IInteractorRegistry
    {
        private readonly List<IInteractorView> _interactors = new();

        public IEnumerable<IInteractorView> AllInteractors => _interactors;

        public void Register(IInteractorView interactor)
        {
            if (!_interactors.Contains(interactor))
            {
                _interactors.Add(interactor);
            }
        }

        public void Unregister(IInteractorView interactor)
        {
            _interactors.Remove(interactor);
        }
    }
}
