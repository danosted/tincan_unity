#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Encounters.Tests
{
    /// <summary>
    /// An encounter zone's round (<see cref="EncounterZoneProcessor"/>, <see cref="EncounterZoneUseCase"/>), the zone
    /// query (<see cref="EncounterZoneQuery"/>) and the HUD line (<see cref="EncounterHudPresenter"/>).
    /// </summary>
    public class EncounterZoneTests
    {
        private sealed class Zone : IEncounterZone
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public string DisplayName => "Storm";
            public Vector2 Centre { get; set; }
            public float Radius { get; set; } = 100f;
            public EncounterZoneState State { get; private set; }
            public double StateEndsAt { get; private set; }
            public void ServerSetCentre(Vector2 centre) => Centre = centre;

            public void ServerSetState(EncounterZoneState state, double endsAt)
            {
                State = state;
                StateEndsAt = endsAt;
            }
        }

        private sealed class Spawner : IEncounterZoneSpawner
        {
            public int Spawns;
            public void Spawn(GameObject prefab, Vector3 position) => Spawns++;
        }

        private sealed class Recorder : IEventPublisher
        {
            public readonly List<EncounterZoneChangedEvent> Changes = new();

            public void Publish<TEvent>(TEvent evt)
            {
                if (evt is EncounterZoneChangedEvent change) Changes.Add(change);
            }
        }

        private EncounterZoneConfig _config = null!;
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private FakeAirshipView _ship = null!;
        private Zone _zone = null!;
        private Recorder _events = null!;
        private EncounterZoneUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EncounterZoneConfig>();
            _config.WarningSeconds = 8f;
            _config.RestSeconds = 60f;
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService();
            _ship = new FakeAirshipView();
            _zone = new Zone { Centre = new Vector2(0f, 1000f) };
            _actors.Register(_ship);
            _actors.Register(_zone);
            _events = new Recorder();
            _useCase = new EncounterZoneUseCase(_config, new FakeNetworkService(), _actors, _time, new Spawner(), _events);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        private void At(float seconds, Vector3 ship)
        {
            _time.Time = seconds;
            _ship.Transform.position = ship;
            _useCase.Tick();
        }

        private static readonly Vector3 Outside = new(0f, 40f, 0f);
        private static readonly Vector3 Inside = new(0f, 40f, 950f);

        [Test]
        public void SailingIn_Warns_ThenRuns()
        {
            At(0f, Outside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Idle));

            At(1f, Inside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Warning));
            Assert.That(_zone.StateEndsAt, Is.EqualTo(9d));

            At(9f, Inside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Running));
        }

        [Test]
        public void TurningBack_DuringTheWarning_StartsNothing()
        {
            At(1f, Inside);
            At(4f, Outside);

            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Idle));
            At(20f, Outside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Idle));
        }

        [Test]
        public void SailingOut_WhileItRuns_Abandons_ThenItRests_AndReturns()
        {
            At(1f, Inside);
            At(9f, Inside);
            At(10f, Outside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Resting));
            Assert.That(_zone.StateEndsAt, Is.EqualTo(70d));

            At(30f, Inside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Resting), "a resting zone does not warn");

            At(70f, Outside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Idle));
        }

        [Test]
        public void SetToGone_AnEndedZone_StaysSpent()
        {
            _config.AfterEnd = EncounterAfterEnd.Gone;
            At(1f, Inside);
            At(9f, Inside);
            At(10f, Outside);

            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Gone));
            At(10000f, Inside);
            Assert.That(_zone.State, Is.EqualTo(EncounterZoneState.Gone));
        }

        [Test]
        public void EachChange_IsAnnounced_Once()
        {
            At(1f, Inside);
            At(2f, Inside);
            At(9f, Inside);

            Assert.That(_events.Changes.Count, Is.EqualTo(2));
            Assert.That(_events.Changes[1].To, Is.EqualTo(EncounterZoneState.Running));
        }

        [Test]
        public void Depth_IsZeroAtTheWall_OneAtTheCentre_AtAnyHeight()
        {
            var centre = new Vector2(0f, 1000f);
            Assert.That(EncounterZoneProcessor.Depth(centre, 100f, new Vector3(0f, 0f, 1000f)), Is.EqualTo(1f));
            Assert.That(EncounterZoneProcessor.Depth(centre, 100f, new Vector3(0f, 5000f, 950f)), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(EncounterZoneProcessor.Depth(centre, 100f, new Vector3(0f, 0f, 1200f)), Is.Zero);
        }

        [Test]
        public void TheQuery_FindsTheZone_AndTheHudSaysWhatHappens()
        {
            var query = new EncounterZoneQuery(_actors);
            Assert.That(query.ZoneAt(Outside), Is.Null);
            Assert.That(EncounterHudPresenter.Describe(query.ZoneAt(Outside), 0d), Is.Empty);

            At(1f, Inside);
            var at = query.ZoneAt(Inside);
            Assert.That(at?.Zone, Is.SameAs(_zone));
            Assert.That(EncounterHudPresenter.Describe(at, 3.5d), Is.EqualTo("Storm ahead: 6 s"));

            At(9f, Inside);
            Assert.That(EncounterHudPresenter.Describe(query.ZoneAt(Inside), 9d), Is.EqualTo("In the storm"));
        }
    }
}
