#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Carry
{
    /// <summary>What a player can hold. Byte-sized so it fits a NetworkVariable cheaply; room for item ids later.</summary>
    public enum CarriedItem : byte
    {
        None = 0,
        JerryCan = 1,
        Net = 2
    }

    /// <summary>
    /// Server-authoritative "what am I holding" state of a player. One item at a time; visuals are the mediator's job.
    /// </summary>
    public interface ICarrier
    {
        CarriedItem Carried { get; }
        bool IsCarrying => Carried != CarriedItem.None;

        bool TryPickUp(CarriedItem item);
        bool TryDrop();
    }

    public static class CarrierLocator
    {
        /// <summary>Resolves the carrier for an interaction requester (the player actor or a sibling component).</summary>
        public static ICarrier? Resolve(IActor? requester) => requester switch
        {
            ICarrier carrier => carrier,
            Component component when component != null => component.GetComponent<ICarrier>(),
            _ => null
        };
    }
}
