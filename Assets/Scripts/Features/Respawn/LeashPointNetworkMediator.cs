#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Ship;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Infrastructure Layer: a ship's leash point, a networked fixture (with an EntityNetworkMediator). It holds no state:
    /// who hangs on it is the server's (<see cref="LeashStore"/>), shown to everyone through the leashed tag. Its children
    /// "Anchor" (the rope's end, outside the rail) and "Stand" (on deck) place it; without them, the root serves both.
    /// </summary>
    public class LeashPointNetworkMediator : NetworkBehaviour, ILeashPoint
    {
        private const string AnchorName = "Anchor";
        private const string StandName = "Stand";

        private ActorIdentity? _identity;
        private Transform? _anchor;
        private Transform? _stand;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public Transform Anchor => _anchor != null ? _anchor : _anchor = transform.FindDescendant(AnchorName) ?? transform;
        public Transform Stand => _stand != null ? _stand : _stand = transform.FindDescendant(StandName) ?? transform;

        public Transform? Ship
        {
            get
            {
                var ship = transform.parent != null ? transform.parent.GetComponentInParent<IAirshipView>() : null;
                return ship?.Transform;
            }
        }
    }
}
