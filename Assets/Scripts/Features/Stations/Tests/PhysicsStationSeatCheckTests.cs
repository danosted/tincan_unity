#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Features.Stations;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Stations.Tests
{
    /// <summary>
    /// <see cref="PhysicsStationSeatCheck"/> against real colliders: a seat needs a floor just under it and room for the
    /// body; the station's own colliders never block it. The seat is the body's root, 1 m above the floor.
    /// </summary>
    public class PhysicsStationSeatCheckTests
    {
        private sealed class FakeStation : MonoBehaviour, IStation
        {
            public Transform? Seat => null;
            public AbilityDefinition? OccupyAbility => null;
            public IReadOnlyList<AbilityDefinition> GrantedAbilities => new List<AbilityDefinition>();
            public Camera? ViewCamera => null;
            public ulong? OccupantClientId => null;
            public InteractionDefinition Definition => null!;
            public void ServerSetOccupant(IHumanoidCharacterView? occupant) { }
        }

        private readonly List<GameObject> _objects = new();
        private FakeHumanoidMovementView _body = null!;
        private FakeHumanoidCharacterView _player = null!;
        private FakeStation _station = null!;
        private Transform _seat = null!;
        private readonly PhysicsStationSeatCheck _check = new();

        [SetUp]
        public void SetUp()
        {
            _body = new FakeHumanoidMovementView("Player");
            _body.SetPose(new Vector3(50f, 1f, 0f), Quaternion.identity);
            _player = new FakeHumanoidCharacterView(_body);
            var station = new GameObject("Station");
            _objects.Add(station);
            _station = station.AddComponent<FakeStation>();
            _seat = new GameObject("Seat").transform;
            _seat.SetParent(station.transform, false);
            _seat.localPosition = new Vector3(0f, 1.05f, -1.8f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _objects) Object.DestroyImmediate(instance);
            _objects.Clear();
            _body.Destroy();
        }

        [Test]
        public void ASeatOverTheDeck_IsSafe()
        {
            Box("Deck", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));

            Assert.That(Check(), Is.True);
        }

        [Test]
        public void ASeatPastTheEdge_IsNotSafe()
        {
            Box("Deck", new Vector3(0f, -0.5f, 2f), new Vector3(10f, 1f, 4f));

            Assert.That(Check(), Is.False, "the deck ends at z 0; the seat is 1.8 m behind it");
        }

        [Test]
        public void ASeatOverALowerDeck_IsNotSafe()
        {
            Box("Lower deck", new Vector3(0f, -3.5f, 0f), new Vector3(10f, 1f, 10f));

            Assert.That(Check(), Is.False);
        }

        [Test]
        public void ASeatInsideAWall_IsNotSafe_ButTheStationsOwnColliderDoesNotBlock()
        {
            Box("Deck", new Vector3(0f, -0.5f, 0f), new Vector3(10f, 1f, 10f));
            var own = Box("Station base", _seat.position, Vector3.one);
            own.transform.SetParent(_station.transform, true);
            Assert.That(Check(), Is.True, "the station's own collider is ignored");

            Box("Wall", _seat.position + Vector3.up * 0.5f, new Vector3(2f, 1f, 0.2f));
            Assert.That(Check(), Is.False);
        }

        private bool Check()
        {
            Physics.SyncTransforms();
            return _check.CanSeat(_player, _station, _seat);
        }

        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            _objects.Add(box);
            box.transform.position = position;
            box.AddComponent<BoxCollider>().size = size;
            return box;
        }
    }
}
