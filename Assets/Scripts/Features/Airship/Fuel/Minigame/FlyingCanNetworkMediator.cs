#nullable enable
using TinCan.Network.Infrastructure;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Infrastructure Layer: a stationary world pickup. NetworkTransformMediator replicates its spawn position.
    /// </summary>
    [RequireComponent(typeof(NetworkTransformMediator))]
    public class FlyingCanNetworkMediator : NetworkMediator, IFlyingCanView
    {
        public override bool IsSimulating => IsSpawned && IsServer;

        public Transform Transform => transform;
    }
}
