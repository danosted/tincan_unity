#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class FuelMotorStatusViewTests
    {
        [TestCase(100f, 100f, false)]
        [TestCase(5.01f, 100f, false)]
        [TestCase(5f, 100f, true)]
        [TestCase(4.99f, 100f, true)]
        [TestCase(0f, 100f, true)]
        [TestCase(-1f, 100f, true)]
        [TestCase(1f, 20f, true)]
        [TestCase(1.01f, 20f, false)]
        [TestCase(0f, 0f, true)]
        public void IsWarning_UsesFivePercentOfCapacity(float level, float capacity, bool expected)
        {
            Assert.That(FuelMotorStatusView.IsWarning(level, capacity), Is.EqualTo(expected));
        }

        [Test]
        public void RefreshVisuals_InitialLowFuelAndRefill_UpdatePlateAndLampWithoutChangingTank()
        {
            var root = new GameObject("Tank");
            try
            {
                var tank = root.AddComponent<FakeFuelTankBehaviour>();
                tank.Inner.Level = 5f;
                var motor = new GameObject("Motor");
                motor.SetActive(false);
                motor.transform.SetParent(root.transform, false);
                var plate = new GameObject("FuelMotor_EmptyPlate");
                plate.transform.SetParent(motor.transform, false);
                plate.SetActive(false);
                var text = new GameObject("FuelMotor_EmptyText");
                text.transform.SetParent(plate.transform, false);
                text.SetActive(false);
                var lamp = new GameObject("FuelStatusLamp");
                lamp.transform.SetParent(motor.transform, false);
                var renderer = lamp.AddComponent<MeshRenderer>();
                var view = motor.AddComponent<FuelMotorStatusView>();
                view.RefreshVisuals();
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                Assert.That(plate.activeSelf && text.activeSelf, Is.True);
                Assert.That(properties.GetColor("_BaseColor").r, Is.GreaterThan(properties.GetColor("_BaseColor").g));
                Assert.That(tank.IsEmpty, Is.False, "A warning must not stall a tank that still contains fuel.");

                tank.Refill(1f);
                view.RefreshVisuals();
                renderer.GetPropertyBlock(properties);
                Assert.That(plate.activeSelf || text.activeSelf, Is.False);
                Assert.That(properties.GetColor("_BaseColor").g, Is.GreaterThan(properties.GetColor("_BaseColor").r));
                Assert.That(tank.Level, Is.EqualTo(6f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
