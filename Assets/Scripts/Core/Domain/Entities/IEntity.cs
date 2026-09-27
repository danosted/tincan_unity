#nullable enable
using System;
using UnityEngine;

namespace TinCan.Core.Domain.Entities
{
    /// <summary>
    /// One networked object in the world (one per NetworkObject). It owns the object's identity and is the only thing
    /// that registers the object's actors and capabilities (see ARCHITECTURE.md, "Entities").
    /// </summary>
    public interface IEntity
    {
        /// <summary>The stable id; <see cref="Guid.Empty"/> on a client before the object has spawned.</summary>
        Guid EntityId { get; }

        /// <summary>The object whose hierarchy holds this entity's actors and capabilities.</summary>
        GameObject Root { get; }
    }
}
