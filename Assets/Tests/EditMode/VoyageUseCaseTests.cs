#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.UI;
using TinCan.Features.Voyage;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="VoyageUseCase"/> (the session loop) and <see cref="RestartVoyageMenuCommand"/>. Plan:
    /// <c>voyage-session.md</c>.
    /// </summary>
    public class VoyageUseCaseTests
    {
        private sealed class FakeVoyageState : IVoyageState
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public VoyagePhase Phase { get; private set; }
            public int Voyage { get; private set; }
            public Vector3 Destination { get; private set; }
            public int BriefingSecondsLeft { get; private set; }
            public bool RestartRequested;

            public void RequestRestart() => RestartRequested = true;

            public bool ConsumeRestartRequest()
            {
                bool requested = RestartRequested;
                RestartRequested = false;
                return requested;
            }

            public void ServerSetPhase(VoyagePhase phase) => Phase = phase;
            public void ServerSetVoyage(int voyage) => Voyage = voyage;
            public void ServerSetDestination(Vector3 destination) => Destination = destination;
            public void ServerSetBriefingSecondsLeft(int seconds) => BriefingSecondsLeft = seconds;
        }

        private sealed class RecordingParticipant : ISessionParticipant
        {
            public List<string> Calls { get; } = new();
            public void ResetForSession() => Calls.Add("reset");
            public void SetSessionActive(bool active) => Calls.Add(active ? "on" : "off");
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private RecordingPublisher _events = null!;
        private VoyageConfig _config = null!;
        private FakeVoyageState _state = null!;
        private RecordingParticipant _participant = null!;
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _ship = null!;
        private HealthAttribute _health = null!;
        private MaxHealthAttribute _maxHealth = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { DeltaTime = 1f };
            _actors = new FakeActorRegistry();
            _events = new RecordingPublisher();
            _config = ScriptableObject.CreateInstance<VoyageConfig>();
            _config.BriefingSeconds = 3f;
            _config.RouteLength = 1000f;
            _config.ArrivalRadius = 50f;
            _state = new FakeVoyageState();
            _participant = new RecordingParticipant();

            _health = ScriptableObject.CreateInstance<HealthAttribute>();
            _health.name = "Attr_Health";
            _maxHealth = ScriptableObject.CreateInstance<MaxHealthAttribute>();
            _maxHealth.name = "Attr_MaxHealth";
            _shipController = new FakeAbilityController();
            var health = new HealthAttributeSet(_shipController, _health, _maxHealth);
            health.InitializeBaseValues(1000f);
            _shipController.RegisterAttributeSet(health);
            _ship = new FakeAirshipView("Airship", _shipController);

            _actors.Register(_ship);
            _actors.Register(_state);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
            UnityEngine.Object.DestroyImmediate(_health);
            UnityEngine.Object.DestroyImmediate(_maxHealth);
        }

        [Test]
        public void BeforeTheStateExists_ThePressureIsAlreadyOff()
        {
            _actors.Unregister(_state);
            var voyage = UseCase();

            voyage.Tick();
            voyage.Tick();

            Assert.That(_participant.Calls, Is.EqualTo(new[] { "off" }), "once, on the first server tick");
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));
        }

        [Test]
        public void FirstTick_Begins_ResetsTheWorld_HoldsThePressure_AndSetsADestinationAhead()
        {
            var voyage = UseCase();

            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
            Assert.That(_participant.Calls, Is.EqualTo(new[] { "off", "reset", "off" }));
            Assert.That(Vector3.Distance(_state.Destination, new Vector3(0f, 0f, 1000f)), Is.LessThan(1e-2f), "straight ahead of the ship");
            Assert.That(_state.BriefingSecondsLeft, Is.EqualTo(3));
            Assert.That(_events.Events.OfType<VoyageStartedEvent>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void AutoStartOff_WaitsForARestart()
        {
            _config.AutoStart = false;
            var voyage = UseCase();

            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));

            _state.RequestRestart();
            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
        }

        [Test]
        public void Briefing_CountsDown_ThenCastsOff_AndSwitchesThePressureOn()
        {
            var voyage = UseCase();
            voyage.Tick(); // begin

            voyage.Tick();
            Assert.That(_state.BriefingSecondsLeft, Is.EqualTo(2));
            voyage.Tick();
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Underway));
            Assert.That(_state.BriefingSecondsLeft, Is.Zero);
            Assert.That(_participant.Calls.Last(), Is.EqualTo("on"));
        }

        [Test]
        public void Underway_ReachingTheDestination_IsArrived()
        {
            var voyage = Underway();

            _ship.Transform.position = new Vector3(0f, 0f, 960f);
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Arrived));
            Assert.That(_participant.Calls.Last(), Is.EqualTo("off"));
            Assert.That(_events.Events.OfType<VoyageEndedEvent>().Single().Outcome, Is.EqualTo(VoyagePhase.Arrived));
        }

        [Test]
        public void Underway_TheShipsHealthRunningOut_IsLost()
        {
            var voyage = Underway();

            _shipController.SetAttribute(_health, new AttributeValue(0f));
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Lost));
            Assert.That(_participant.Calls.Last(), Is.EqualTo("off"));
        }

        [Test]
        public void Restart_AfterTheEnd_BeginsANewVoyage_FromWhereTheShipIs()
        {
            var voyage = Underway();
            _ship.Transform.position = new Vector3(0f, 0f, 990f);
            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Arrived));

            _state.RequestRestart();
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
            Assert.That(_state.Voyage, Is.EqualTo(2));
            Assert.That(Vector3.Distance(_state.Destination, new Vector3(0f, 0f, 1990f)), Is.LessThan(1e-2f));
            Assert.That(_participant.Calls.Count(call => call == "reset"), Is.EqualTo(2));
        }

        [Test]
        public void Ended_StaysEnded_WithoutARestart()
        {
            var voyage = Underway();
            _shipController.SetAttribute(_health, new AttributeValue(0f));
            voyage.Tick();

            for (int i = 0; i < 5; i++) voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Lost));
        }

        [Test]
        public void RestartMenuCommand_AsksTheStateForARestart()
        {
            var command = new RestartVoyageMenuCommand(_actors);

            command.Execute(new MenuContext(null!, "voyage_lost", "restart"));

            Assert.That(_state.RestartRequested, Is.True);
            Assert.That(command.CommandId, Is.EqualTo(RestartVoyageMenuCommand.Id));
        }

        private VoyageUseCase Underway()
        {
            var voyage = UseCase();
            voyage.Tick();
            Assert.That(((IVoyage)voyage).CastOff(), Is.True);
            return voyage;
        }

        private VoyageUseCase UseCase() =>
            new(new FakeNetworkService(), _actors, _time, _events, new VoyageRouteProcessor(), _config, new ISessionParticipant[] { _participant });
    }
}
