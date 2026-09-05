#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>A jerry can flying past the ship. Position is server-driven and replicated by a NetworkTransform.</summary>
    public interface IFlyingCanView : IActor
    {
        Transform Transform { get; }
        Vector3 Velocity { get; set; }
        float SpawnTime { get; set; }
    }

    /// <summary>Server-side creation/destruction of networked flying cans.</summary>
    public interface IFlyingCanSpawner
    {
        IFlyingCanView? Spawn(Vector3 position, Vector3 velocity);
        void Despawn(IFlyingCanView can);
    }
}
