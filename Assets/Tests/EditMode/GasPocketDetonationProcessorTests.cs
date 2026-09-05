#nullable enable
using NUnit.Framework;
using TinCan.Features.GasChallenge;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class GasPocketDetonationProcessorTests
    {
        private GameObject _pocketObject = null!;
        private GameObject _shipObject = null!;
        private GasPocketVolume _pocket = null!;
        private SphereCollider _shipCollider = null!;

        [SetUp]
        public void SetUp()
        {
            _pocketObject = new GameObject("GasPocket");
            var pocketCollider = _pocketObject.AddComponent<SphereCollider>();
            pocketCollider.radius = 3f;
            pocketCollider.center = Vector3.zero;
            _pocket = _pocketObject.AddComponent<GasPocketVolume>();
            _pocket.SetRadius(3f);

            _shipObject = new GameObject("Airship");
            _shipCollider = _shipObject.AddComponent<SphereCollider>();
            _shipCollider.radius = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_pocketObject);
            Object.DestroyImmediate(_shipObject);
        }

        [Test]
        public void ShouldDetonate_WhenShipColliderOverlapsPocket()
        {
            _shipCollider.center = new Vector3(1f, 0f, 0f);

            Assert.That(GasPocketDetonationProcessor.ShouldDetonate(_pocket, _shipCollider), Is.True);
        }

        [Test]
        public void ShouldNotDetonate_WhenShipIsOutsidePocket()
        {
            _shipObject.transform.position = new Vector3(20f, 0f, 0f);

            Assert.That(GasPocketDetonationProcessor.ShouldDetonate(_pocket, _shipCollider), Is.False);
        }

        [Test]
        public void ShouldNotDetonate_WhenPocketAlreadyDetonated()
        {
            _shipCollider.center = new Vector3(1f, 0f, 0f);
            _pocket.MarkDetonated();

            Assert.That(GasPocketDetonationProcessor.ShouldDetonate(_pocket, _shipCollider), Is.False);
        }

        [Test]
        public void ShouldNotDetonate_WhenColliderIsMissing()
        {
            Assert.That(GasPocketDetonationProcessor.ShouldDetonate(_pocket, null), Is.False);
        }
    }
}
