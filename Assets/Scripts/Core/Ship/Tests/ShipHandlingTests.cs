#nullable enable
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TinCan.Core.Ship.Tests
{
    /// <summary>
    /// <see cref="AirshipControllerView"/> flies on its handling: the profile it names, else the default, and the profile
    /// the server sets for a designed ship over both; an edit to the profile it flies on applies at once (tuned live;
    /// plan .docs/plans/ship-handling-profiles.md).
    /// </summary>
    public class ShipHandlingTests
    {
        private GameObject _ship = null!;
        private AirshipControllerView _view = null!;
        private ShipHandlingProfile _profile = null!;

        [SetUp]
        public void SetUp()
        {
            _ship = new GameObject("Ship");
            _view = _ship.AddComponent<AirshipControllerView>();
            var handling = ShipHandling.Default;
            handling.MaxForwardSpeed = 22f;
            handling.PitchSpeed = 11f;
            _profile = ShipHandlingProfile.Create("HANDLING_Test", handling);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ship);
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void WithNoProfile_TheShipFliesOnTheDefault()
        {
            Assert.That(_view.Handling, Is.EqualTo(ShipHandling.Default));
            Assert.That(_view.MaxForwardSpeed, Is.EqualTo(ShipHandling.Default.MaxForwardSpeed));
        }

        [Test]
        public void TheShip_ReadsEveryValue_FromItsProfile()
        {
            var serialized = new SerializedObject(_view);
            serialized.FindProperty("_handlingProfile").objectReferenceValue = _profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(_view.Handling, Is.EqualTo(_profile.Handling));
            Assert.That((_view.MaxForwardSpeed, _view.PitchSpeed), Is.EqualTo((22f, 11f)));
        }

        [Test]
        public void HandlingSetAtRuntime_ComesBeforeTheProfile()
        {
            var serialized = new SerializedObject(_view);
            serialized.FindProperty("_handlingProfile").objectReferenceValue = _profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var handling = ShipHandling.Default;
            handling.TurnSpeed = 7f;
            var core = ShipHandlingProfile.Create("HANDLING_Core", handling);
            var changes = 0;
            _view.HandlingChanged += () => changes++;

            _view.SetHandling(core);

            Assert.That((_view.TurnSpeed, _view.MaxForwardSpeed), Is.EqualTo((7f, ShipHandling.Default.MaxForwardSpeed)));
            Assert.That(changes, Is.EqualTo(1));
            Object.DestroyImmediate(core);
        }

        [Test]
        public void TuningTheProfileItFliesOn_AppliesAtOnce()
        {
            _view.SetHandling(_profile);
            var changes = 0;
            _view.HandlingChanged += () => changes++;
            var tuned = _profile.Handling;
            tuned.MaxForwardSpeed = 40f;
            tuned.BankSpeed = 6f;

            _profile.SetHandling(tuned);

            Assert.That((_view.MaxForwardSpeed, _view.BankSpeed, changes), Is.EqualTo((40f, 6f, 1)));
        }

        [Test]
        public void TuningAProfileItNoLongerFliesOn_ChangesNothing()
        {
            var other = ShipHandlingProfile.Create("HANDLING_Other", ShipHandling.Default);
            _view.SetHandling(_profile);
            _view.SetHandling(other);
            var changes = 0;
            _view.HandlingChanged += () => changes++;

            _profile.SetHandling(ShipHandling.Default);

            Assert.That(changes, Is.Zero);
            Object.DestroyImmediate(other);
        }
    }
}
