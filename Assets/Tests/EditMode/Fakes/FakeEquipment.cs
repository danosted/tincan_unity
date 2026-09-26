#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Carry;
using TinCan.Features.Items;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A requester that is its own equipment (the shape handlers see when the player actor implements IEquipment).</summary>
    public class FakeEquipmentActor : IActor, IEquipment
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public ItemDefinition? Held { get; set; }
        public int Equips { get; private set; }
        public int Unequips { get; private set; }

        public bool TryEquip(ItemDefinition item)
        {
            if (Held != null) return false;
            Held = item;
            Equips++;
            return true;
        }

        public bool TryUnequip()
        {
            if (Held == null) return false;
            Held = null;
            Unequips++;
            return true;
        }
    }

    public class FakeJerryCanSupply : IInteractable, IJerryCanSupply
    {
        public int Count { get; set; }
        public ItemDefinition? Item { get; set; }

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
        public ItemDefinition? Item => Inner.Item;
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
