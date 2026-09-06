#nullable enable
using TinCan.Features.Airship.Fuel;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Plain fuel tank fake with recording; no server guard, so tests observe every write.</summary>
    public class FakeFuelTank : IFuelTank
    {
        public float Level { get; set; } = 100f;
        public float Capacity { get; set; } = 100f;
        public bool IsEmpty => Level <= 0f;
        public FuelConfig? Config { get; set; }
        public float TotalConsumed { get; private set; }
        public int RefillCalls { get; private set; }

        public void Consume(float amount)
        {
            TotalConsumed += amount;
            Level = Mathf.Clamp(Level - amount, 0f, Capacity);
        }

        public float Refill(float amount)
        {
            RefillCalls++;
            float before = Level;
            Level = Mathf.Clamp(Level + amount, 0f, Capacity);
            return Level - before;
        }
    }

    /// <summary>Component wrapper so a FakeFuelTank can sit under a FakeAirshipView's GameObject and be located.</summary>
    public class FakeFuelTankBehaviour : MonoBehaviour, IFuelTank
    {
        public FakeFuelTank Inner { get; } = new();

        public float Level => Inner.Level;
        public float Capacity => Inner.Capacity;
        public bool IsEmpty => Inner.IsEmpty;
        public FuelConfig? Config => Inner.Config;
        public void Consume(float amount) => Inner.Consume(amount);
        public float Refill(float amount) => Inner.Refill(amount);

        public static FakeFuelTankBehaviour AttachTo(GameObject parent, FuelConfig? config)
        {
            var child = new GameObject("FuelSystem");
            child.transform.SetParent(parent.transform, false);
            var tank = child.AddComponent<FakeFuelTankBehaviour>();
            tank.Inner.Config = config;
            return tank;
        }
    }
}
