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
        public void RefreshVisuals_LowEmptyAndRefill_UpdateRunesAndWindIndependently()
        {
            var root = new GameObject("Tank");
            try
            {
                var tank = root.AddComponent<FakeFuelTankBehaviour>();
                tank.Inner.Level = 5f;
                var motor = new GameObject("Receiver");
                motor.SetActive(false);
                motor.transform.SetParent(root.transform, false);
                var core = AddNode(motor, "FuelMotor_DialCore").AddComponent<MeshRenderer>();
                var rune = AddNode(motor, "FuelMotor_DialRune.007").AddComponent<MeshRenderer>();
                var firstWind = AddNode(motor, "FuelMotor_Heart_Wind_1");
                var secondWind = AddNode(motor, "FuelMotor_Heart_Wind_2");
                var mote = AddNode(firstWind, "FuelMotor_Heart_Mote");
                firstWind.SetActive(false);
                secondWind.SetActive(false);
                var view = motor.AddComponent<FuelMotorStatusView>();
                view.RefreshVisuals();
                AssertWarning(core, true);
                AssertWarning(rune, true);
                Assert.That(firstWind.activeSelf && secondWind.activeSelf, Is.True);
                Assert.That(tank.IsEmpty, Is.False, "The 5% warning must not extinguish a powered receiver.");

                tank.Inner.Level = 0f; // Warning remains true, but wind must still switch off.
                view.RefreshVisuals();
                Assert.That(firstWind.activeSelf || secondWind.activeSelf, Is.False);
                Assert.That(mote.activeInHierarchy, Is.False);
                AssertWarning(core, true);

                tank.Refill(1f); // Wind returns even though the warning remains true.
                view.RefreshVisuals();
                Assert.That(firstWind.activeSelf && secondWind.activeSelf, Is.True);
                AssertWarning(core, true);
                tank.Refill(5f);
                view.RefreshVisuals();
                AssertWarning(core, false);
                AssertWarning(rune, false);
                Assert.That(tank.Level, Is.EqualTo(6f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RefreshVisuals_InitiallyEmptyReceiver_HidesAuthoredPoweredWind()
        {
            var root = new GameObject("Tank");
            try
            {
                root.SetActive(false);
                var tank = root.AddComponent<FakeFuelTankBehaviour>();
                tank.Inner.Level = 0f;
                var wind = AddNode(root, "FuelMotor_Heart_Wind_1");
                var view = root.AddComponent<FuelMotorStatusView>();
                view.RefreshVisuals();
                Assert.That(wind.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject AddNode(GameObject parent, string name)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent.transform, false);
            return node;
        }

        private static void AssertWarning(Renderer renderer, bool expected)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color color = properties.GetColor("_BaseColor");
            Assert.That(color.r > color.g, Is.EqualTo(expected));
            Assert.That(properties.GetColor("_EmissionColor"), Is.EqualTo(color));
        }
    }
}
