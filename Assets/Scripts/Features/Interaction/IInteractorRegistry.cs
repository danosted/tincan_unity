using System.Collections.Generic;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Registry for all active interactor views in the scene.
    /// This allows decoupling interaction logic from the main actor registry.
    /// </summary>
    public interface IInteractorRegistry
    {
        IEnumerable<IInteractorView> AllInteractors { get; }
        void Register(IInteractorView interactor);
        void Unregister(IInteractorView interactor);
    }
}
