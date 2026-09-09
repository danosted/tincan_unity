using NUnit.Framework;
using UnityEngine;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Tests.EditMode
{
    public class HumanoidPredictionReconciliationTests
    {
        [Test]
        public void CalculateBlendFactor_WithPositiveValues_ReturnsFractionBetweenZeroAndOne()
        {
            float blend = HumanoidPredictionReconciliation.CalculateBlendFactor(14f, 1f / 60f);

            Assert.That(blend, Is.GreaterThan(0f));
            Assert.That(blend, Is.LessThan(1f));
        }

        [Test]
        public void CalculateBlendFactor_WithNonPositiveInputs_ReturnsOne()
        {
            Assert.That(HumanoidPredictionReconciliation.CalculateBlendFactor(0f, 1f / 60f), Is.EqualTo(1f));
            Assert.That(HumanoidPredictionReconciliation.CalculateBlendFactor(14f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void TryComputePositionError_WhenDifferentPositions_ReturnsTrueAndDelta()
        {
            bool hasCorrection = HumanoidPredictionReconciliation.TryComputePositionError(
                new Vector3(10f, 1f, -3f),
                new Vector3(7f, 1f, -2f),
                out var correction);

            Assert.That(hasCorrection, Is.True);
            Assert.That(correction, Is.EqualTo(new Vector3(3f, 0f, -1f)));
        }

        [Test]
        public void TryComputePositionError_WhenEqualPositions_ReturnsFalseAndZero()
        {
            bool hasCorrection = HumanoidPredictionReconciliation.TryComputePositionError(
                new Vector3(5f, 2f, 9f),
                new Vector3(5f, 2f, 9f),
                out var correction);

            Assert.That(hasCorrection, Is.False);
            Assert.That(correction, Is.EqualTo(Vector3.zero));
        }
    }
}
