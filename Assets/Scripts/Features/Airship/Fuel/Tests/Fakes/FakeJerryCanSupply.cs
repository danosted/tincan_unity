#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Items;
using TinCan.Features.Airship.Fuel;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Airship.Fuel.Tests
{
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

    /// <summary>
    /// Component wrapper so a supply can sit under a FakeAirshipView and be located like the real crate. It shares
    /// this file with the plain class above on purpose (see FakeShipDamage.cs).
    /// </summary>
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
}
