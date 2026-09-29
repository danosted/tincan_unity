#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Entities;

namespace TinCan.Core.Entities
{
    /// <summary>The live entities by id. Registering the same entity twice is a no-op, which makes registration idempotent.</summary>
    public sealed class EntityRegistry : IEntityRegistry
    {
        private readonly Dictionary<Guid, IEntity> _byId = new();

        public IEnumerable<IEntity> All => _byId.Values;

        public bool TryGet(Guid id, out IEntity entity) => _byId.TryGetValue(id, out entity!);

        public bool Register(IEntity entity)
        {
            var id = entity.EntityId;
            if (id == Guid.Empty || _byId.ContainsKey(id)) return false;
            _byId.Add(id, entity);
            return true;
        }

        public bool Unregister(IEntity entity)
        {
            var id = entity.EntityId;
            if (!_byId.TryGetValue(id, out var registered) || !ReferenceEquals(registered, entity)) return false;
            _byId.Remove(id);
            return true;
        }
    }
}
