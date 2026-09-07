#nullable enable
using TinCan.Features.Airship.Fuel.Minigame;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: a stationary world pickup. NetworkTransformMediator replicates its spawn position.
    /// </summary>
    [RequireComponent(typeof(NetworkTransformMediator))]
    public class FlyingCanNetworkMediator : NetworkMediator, IFlyingCanView
    {
        public override bool IsSimulating => IsSpawned && IsServer;

        public Transform Transform => transform;

        // Debris is never a possession target (NetworkMediator defaults to "possessable when free").
        public override bool CanPossess(ulong playerId) => false;
    }
}
