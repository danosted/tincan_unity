using TinCan.Core.Domain.Entities;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Domain Layer: registers an entity's actors and capabilities (actor, interactors, ability controller,
    /// targetables) in the domain registries, and links ship modules to their ship. Only an entity calls
    /// <see cref="RegisterEntity"/>; registering the same entity again is a no-op.
    /// </summary>
    public interface IActorOrchestrator
    {
        IEntityRegistry Entities { get; }

        void RegisterEntity(IEntity entity);
        void UnregisterEntity(IEntity entity);
        void RegisterShipModule(IShipModule module, IShipModuleRegistry registry);
        void UnregisterShipModule(IShipModule module);
    }
}
