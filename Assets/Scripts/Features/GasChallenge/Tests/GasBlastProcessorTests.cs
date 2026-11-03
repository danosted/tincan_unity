#nullable enable
using NUnit.Framework;
using TinCan.Features.GasChallenge;
using UnityEngine;

namespace TinCan.Features.GasChallenge.Tests
{
    /// <summary>GasBlastProcessor: which damage points a gas blast breaks.</summary>
    public class GasBlastProcessorTests
    {
        private readonly GasBlastProcessor _blast = new();

        [Test]
        public void FittingsFirst_ThenHullSpots_NearestFirst_UpToTheLimit()
        {
            var targets = new[]
            {
                new BlastTarget(0, new Vector3(1f, 0f, 0f), isFitting: false),
                new BlastTarget(1, new Vector3(4f, 0f, 0f), isFitting: true),
                new BlastTarget(2, new Vector3(2f, 0f, 0f), isFitting: true),
                new BlastTarget(3, new Vector3(3f, 0f, 0f), isFitting: false),
            };

            Assert.That(_blast.Pick(Vector3.zero, targets, 5f, 3), Is.EqualTo(new[] { 2, 1, 0 }));
        }

        [Test]
        public void OutOfReach_OrNoBreaksAllowed_BreaksNothing()
        {
            var far = new[] { new BlastTarget(0, new Vector3(9f, 0f, 0f), isFitting: true) };
            Assert.That(_blast.Pick(Vector3.zero, far, 8f, 2), Is.Empty);

            var near = new[] { new BlastTarget(0, Vector3.one, isFitting: true) };
            Assert.That(_blast.Pick(Vector3.zero, near, 8f, 0), Is.Empty);
        }
    }
}
