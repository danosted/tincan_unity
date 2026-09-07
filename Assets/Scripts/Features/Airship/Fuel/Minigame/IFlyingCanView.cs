#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>A stationary world-space jerry can, spawned and collected by the server.</summary>
    public interface IFlyingCanView : IActor
    {
        Transform Transform { get; }
    }

    /// <summary>Server-side creation/destruction of networked flying cans.</summary>
    public interface IFlyingCanSpawner
    {
        IFlyingCanView? Spawn(Vector3 position);
        void Despawn(IFlyingCanView can);
    }
}
