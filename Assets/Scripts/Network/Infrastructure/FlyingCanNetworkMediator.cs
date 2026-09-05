#nullable enable
using TinCan.Features.Airship.Fuel.Minigame;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: a networked flying jerry can. The server moves the transform (FlyingCanUseCase) and
    /// NetworkTransformMediator replicates it. Velocity and spawn time are server-side bookkeeping only.
    /// </summary>
    [RequireComponent(typeof(NetworkTransformMediator))]
    public class FlyingCanNetworkMediator : NetworkMediator, IFlyingCanView
    {
        public override bool IsSimulating => IsSpawned && IsServer;

        public Transform Transform => transform;
        public Vector3 Velocity { get; set; }
        public float SpawnTime { get; set; }

        // Debris is never a possession target (NetworkMediator defaults to "possessable when free").
        public override bool CanPossess(ulong playerId) => false;
    }
}
