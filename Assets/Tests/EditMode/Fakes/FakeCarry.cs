#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Carry;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A requester that is its own carrier (the shape handlers see when the player actor implements ICarrier).</summary>
    public class FakeCarrierActor : IActor, ICarrier
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public CarriedItem Carried { get; set; }
        public int PickUps { get; private set; }
        public int Drops { get; private set; }

        public bool TryPickUp(CarriedItem item)
        {
            if (item == CarriedItem.None || Carried != CarriedItem.None) return false;
            Carried = item;
            PickUps++;
            return true;
        }

        public bool TryDrop()
        {
            if (Carried == CarriedItem.None) return false;
            Carried = CarriedItem.None;
            Drops++;
            return true;
        }
    }

    public class FakeJerryCanSupply : IInteractable, IJerryCanSupply
    {
        public int Count { get; set; }

        public bool TryTake()
        {
            if (Count <= 0) return false;
            Count--;
            return true;
        }

        public void Add(int amount)
        {
            if (amount > 0) Count += amount;
        }
    }

    /// <summary>Component wrapper so a supply can sit under a FakeAirshipView and be located like the real crate.</summary>
    public class FakeJerryCanSupplyBehaviour : MonoBehaviour, IJerryCanSupply
    {
        public FakeJerryCanSupply Inner { get; } = new();
        public int Count => Inner.Count;
        public bool TryTake() => Inner.TryTake();
        public void Add(int amount) => Inner.Add(amount);

        public static FakeJerryCanSupplyBehaviour AttachTo(GameObject parent, int count)
        {
            var child = new GameObject("JerryCanSupply");
            child.transform.SetParent(parent.transform, false);
            var supply = child.AddComponent<FakeJerryCanSupplyBehaviour>();
            supply.Inner.Count = count;
            return supply;
        }
    }

    public sealed class FakeNetRack : IInteractable, INetRack { }
}
