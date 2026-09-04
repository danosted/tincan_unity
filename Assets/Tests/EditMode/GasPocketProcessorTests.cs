#nullable enable
using NUnit.Framework;
using TinCan.Features.GasChallenge;
using TinCan.Features.Abilities;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class GasPocketProcessorTests
    {
        [Test]
        public void HealthValueProcessor_ClampsDamageAndRepairToBounds()
        {
            Assert.That(HealthValueProcessor.ApplyDamage(10f, 100f, 25f), Is.EqualTo(0f));
            Assert.That(HealthValueProcessor.Repair(90f, 100f, 25f), Is.EqualTo(100f));
            Assert.That(HealthValueProcessor.ApplyDamage(50f, 100f, -10f), Is.EqualTo(50f));
            Assert.That(HealthValueProcessor.Repair(50f, 100f, -10f), Is.EqualTo(50f));
        }

        [Test]
        public void EvaluateAirship_WhenShipIsInsidePocket_ReturnsDangerState()
        {
            var pocket = new GasPocketVolumeDefinition(new Vector3(0f, 10f, 0f), 3f, 10f);

            GasPocketResult result = GasPocketProcessor.EvaluateAirship(new GasPocketResult(false, 0f, 0f), new Vector3(1f, 10f, 0f), pocket, 1f);

            Assert.That(result.IsInGas, Is.True);
            Assert.That(result.DamageThisTick, Is.EqualTo(10f));
        }

        [Test]
        public void EvaluateAirship_WhenShipIsOutsidePocket_ReturnsSafeState()
        {
            var pocket = new GasPocketVolumeDefinition(new Vector3(0f, 10f, 0f), 3f, 10f);

            GasPocketResult result = GasPocketProcessor.EvaluateAirship(new GasPocketResult(false, 0f, 0f), new Vector3(6f, 10f, 0f), pocket, 1f);

            Assert.That(result.IsInGas, Is.False);
            Assert.That(result.DamageThisTick, Is.EqualTo(0f));
        }

        [Test]
        public void GasPocketVolume_WhenShipColliderIsInside_ReturnsDangerState()
        {
            var pocketObject = new GameObject("GasPocket");
            var pocketCollider = pocketObject.AddComponent<SphereCollider>();
            pocketCollider.radius = 3f;
            pocketCollider.center = Vector3.zero;
            var pocket = pocketObject.AddComponent<GasPocketVolume>();
            pocket.SetRadius(3f);

            var shipObject = new GameObject("Airship");
            var shipCollider = shipObject.AddComponent<SphereCollider>();
            shipCollider.radius = 1f;
            shipCollider.center = new Vector3(1f, 0f, 0f);

            var result = GasPocketProcessor.EvaluateAirship(
                new GasPocketResult(false, 0f, 0f),
                shipCollider,
                pocket,
                1f);

            Assert.That(result.IsInGas, Is.True);
            Assert.That(result.DamageThisTick, Is.EqualTo(10f));

            Object.DestroyImmediate(pocketObject);
            Object.DestroyImmediate(shipObject);
        }
    }
}
