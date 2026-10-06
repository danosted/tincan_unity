#nullable enable
using System;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using UnityEngine;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// A free socket as something to press E on. Added at runtime on every peer next to each part's
    /// <see cref="ShipSocket"/> marker (<see cref="ShipSocketTargetsUseCase"/>), with a trigger at standing height for the
    /// interaction ray. Taken sockets are not targetable, so the mounted fitting gets the look instead.
    /// </summary>
    public sealed class ShipSocketTarget : MonoBehaviour, IInteractionTarget
    {
        public const string ObjectName = "ShipSocketTarget";

        public InteractionDefinition Definition { get; private set; } = null!;
        public Guid ShipId { get; private set; }
        public ShipSocketId Socket { get; private set; }
        public bool IsFree { get; set; } = true;

        public bool IsTargetable => IsFree && isActiveAndEnabled;

        Vector3 Core.Domain.Targeting.ITargetable.AimPoint => transform.position;
        bool Core.Domain.Targeting.ITargetable.IsTargetable => IsTargetable;

        public void Bind(Guid shipId, ShipSocketId socket, InteractionDefinition definition)
        {
            ShipId = shipId;
            Socket = socket;
            Definition = definition;
        }
    }
}
