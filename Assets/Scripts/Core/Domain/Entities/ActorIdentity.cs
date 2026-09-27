#nullable enable
using System;
using UnityEngine;

namespace TinCan.Core.Domain.Entities
{
    /// <summary>
    /// An actor component's id, taken from its entity (<see cref="EntityIds.ActorIdFor"/>) and cached once known.
    /// A component with no entity above it (local-only objects, EditMode tests) gets a random local id instead.
    /// Use: <c>public Guid Id => (_identity ??= new ActorIdentity(this)).Id;</c>
    /// </summary>
    public sealed class ActorIdentity
    {
        private readonly Component _owner;
        private Guid _id;

        public ActorIdentity(Component owner) => _owner = owner;

        public Guid Id
        {
            get
            {
                if (_id != Guid.Empty) return _id;
                var fromEntity = EntityIds.ActorIdFor(_owner);
                if (fromEntity == null) return _id = EntityIds.New();
                return _id = fromEntity.Value; // stays empty (and is asked again) until the entity has its id
            }
        }
    }
}
