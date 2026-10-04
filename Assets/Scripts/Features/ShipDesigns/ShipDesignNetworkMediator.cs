#nullable enable
using System;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Ship;
using Unity.Netcode;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Infrastructure Layer: a ship's design on the ShipDesignState fixture (with an EntityNetworkMediator, which registers
    /// it as an actor). One server-written variable carries the design's network form and hash to every peer; each peer
    /// builds the ship from it (<see cref="ShipAssemblyUseCase"/>).
    /// </summary>
    public class ShipDesignNetworkMediator : NetworkBehaviour, IShipDesignState
    {
        private readonly NetworkVariable<ShipDesignBlob> _design = new(
            new ShipDesignBlob(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;

        public IAirshipView? Ship => transform.parent != null ? transform.parent.GetComponentInParent<IAirshipView>() : null;

        public ulong DesignHash => _design.Value?.Hash ?? 0;
        public byte[] DesignBytes => _design.Value?.Bytes ?? Array.Empty<byte>();

        public void ServerSetDesign(ulong hash, byte[] bytes)
        {
            if (!IsServer) return;
            _design.Value = new ShipDesignBlob(hash, bytes);
        }
    }
}
