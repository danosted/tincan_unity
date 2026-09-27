#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Entities
{
    /// <summary>
    /// The live entities, by id. A later session layer owns this, so ending a session clears it.
    /// </summary>
    public interface IEntityRegistry
    {
        IEnumerable<IEntity> All { get; }
        bool TryGet(Guid id, out IEntity entity);

        /// <summary>Adds the entity; false when it (or another entity with its id) is already registered.</summary>
        bool Register(IEntity entity);

        /// <summary>Removes the entity; false when it was not registered.</summary>
        bool Unregister(IEntity entity);
    }
}
