#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Ship;
using TinCan.Core.UI;
using TinCan.Features.ShipLoss;
using TinCan.Features.Voyage;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Features.Voyage.Tests
{
    /// <summary>
    /// <see cref="VoyageUseCase"/> (the session loop), <see cref="RestartVoyageMenuCommand"/> and <see cref="VoyageLostChoices"/>. Plan:
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
            public int LayoutSeed { get; private set; }
            public Vector3 Origin { get; private set; }
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

            public void ServerSetLayout(int layoutSeed, Vector3 origin)
            {
                LayoutSeed = layoutSeed;
                Origin = origin;
            }
            public void ServerSetBriefingSecondsLeft(int seconds) => BriefingSecondsLeft = seconds;
        }

        private sealed class RecordingParticipant : ISessionParticipant
        {
            public List<string> Calls { get; } = new();
            public void ResetForSession() => Calls.Add("reset");
            public void SetSessionActive(bool active) => Calls.Add(active ? "on" : "off");
        }

        private sealed class FakeShipLoss : IShipLoss
        {
            public bool IsLost { get; set; }
            public List<Vector3> BroughtBackAt { get; } = new();

            public void BringBack(IAirshipView ship, Vector3 position, Quaternion rotation)
            {
                ship.Transform.SetPositionAndRotation(position, rotation);
                BroughtBackAt.Add(position);
                IsLost = false;
            }
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
        private FakeAirshipView _ship = null!;
        private FakeCrewMember _crew = null!;
        private FakeShipLoss _loss = null!;

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

            _ship = new FakeAirshipView("Airship");

            _crew = new FakeCrewMember();
            _loss = new FakeShipLoss();

            _actors.Register(_ship);
            _actors.Register(_state);
            _actors.Register(_crew);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
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
        public void EveryVoyage_RollsANewLayoutSeed_FromWhereTheShipIs()
        {
            _ship.Transform.position = new Vector3(120f, 40f, -60f);
            var voyage = UseCase();

            voyage.Tick();
            int first = _state.LayoutSeed;
            Assert.That(first, Is.Not.EqualTo(0), "0 means no session layout");
            Assert.That(_state.Origin, Is.EqualTo(new Vector3(120f, 40f, -60f)));

            _ship.Transform.position = new Vector3(500f, 40f, 0f);
            _state.RequestRestart();
            voyage.Tick();

            Assert.That(_state.LayoutSeed, Is.Not.EqualTo(0).And.Not.EqualTo(first));
            Assert.That(_state.Origin, Is.EqualTo(new Vector3(500f, 40f, 0f)));
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
        public void Underway_TheShipLost_IsLost_AndStopsThePressure()
        {
            var voyage = Underway();

            _loss.IsLost = true;
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Lost));
            Assert.That(_participant.Calls.Last(), Is.EqualTo("off"));
            Assert.That(_events.Events.OfType<VoyageEndedEvent>().Single().Outcome, Is.EqualTo(VoyagePhase.Lost));
        }

        [Test]
        public void Lost_TheShipRespawned_CarriesOnTheSameVoyage()
        {
            var voyage = Underway();
            Vector3 destination = _state.Destination;
            _loss.IsLost = true;
            voyage.Tick();

            _loss.IsLost = false;
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Underway));
            Assert.That(_state.Destination, Is.EqualTo(destination), "the same voyage");
            Assert.That(_state.Voyage, Is.EqualTo(1));
            Assert.That(_participant.Calls.Count(call => call == "reset"), Is.EqualTo(1), "the world keeps its progress");
            Assert.That(_participant.Calls.Last(), Is.EqualTo("on"));
        }

        [Test]
        public void Restart_AfterALoss_BringsTheShipBackWhereTheVoyageBegan_AndBeginsANewOne()
        {
            _ship.Transform.position = new Vector3(5f, 20f, 5f);
            var voyage = Underway();
            _ship.Transform.position = new Vector3(5f, -80f, 400f);
            _loss.IsLost = true;
            voyage.Tick();

            _state.RequestRestart();
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
            Assert.That(_state.Voyage, Is.EqualTo(2));
            Assert.That(_loss.BroughtBackAt, Is.EqualTo(new[] { new Vector3(5f, 20f, 5f) }));
            Assert.That(_state.Origin, Is.EqualTo(new Vector3(5f, 20f, 5f)));
        }

        [Test]
        public void AShipLostBeforeAnyVoyage_WaitsForItsRespawn()
        {
            _loss.IsLost = true;
            var voyage = UseCase();

            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));

            _loss.IsLost = false;
            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
        }

        [Test]
        public void LostChoices_OfferRestartVoyage_OnTheLostScreen()
        {
            var choices = new VoyageLostChoices(new RestartVoyageMenuCommand(_actors));

            Assert.That(choices.SourceId, Is.EqualTo(ShipLossMenus.ChoicesSource));
            Assert.That(choices.Rows.Select(row => row.Label), Is.EqualTo(new[] { "Restart Voyage" }));

            choices.Invoke(VoyageLostChoices.RestartKey, new MenuContext(null!, "ship_lost", "choices/restart"));

            Assert.That(_state.RestartRequested, Is.True);
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
            _loss.IsLost = true;
            voyage.Tick();

            for (int i = 0; i < 5; i++) voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Lost));
        }

        [Test]
        public void NobodyAboard_DoesNotBegin()
        {
            _actors.Unregister(_crew);
            _actors.Register(new FakeCrewMember(isPlayerCharacter: false));
            var voyage = UseCase();

            for (int i = 0; i < 5; i++) voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));
            Assert.That(_participant.Calls, Does.Not.Contain("reset"));
        }

        [Test]
        public void BelowMinCrew_Waits_ThenBeginsWhenEnoughAreAboard()
        {
            _config.MinCrew = 2;
            var voyage = UseCase();

            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));

            _actors.Register(new FakeCrewMember());
            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
        }

        [TestCase(false, TestName = "CrewLeaves_DuringTheBriefing_StandsDownToIdle")]
        [TestCase(true, TestName = "CrewLeaves_Underway_StandsDownToIdle_AndReleasesThePressure")]
        public void CrewLeaves_StandsDownToIdle(bool underway)
        {
            var voyage = underway ? Underway() : UseCase();
            if (!underway) voyage.Tick();

            _actors.Unregister(_crew);
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));
            Assert.That(_state.BriefingSecondsLeft, Is.Zero);
            Assert.That(_participant.Calls.Last(), Is.EqualTo("off"));
        }

        [Test]
        public void CrewLeaves_OnTheEndScreen_StandsDownToIdle()
        {
            var voyage = Underway();
            _loss.IsLost = true;
            voyage.Tick();
            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Lost));

            _actors.Unregister(_crew);
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Idle));
        }

        [Test]
        public void AfterAStandDown_TheNextCrew_BeginsAFreshVoyage()
        {
            var voyage = Underway();
            _actors.Unregister(_crew);
            voyage.Tick();

            _actors.Register(new FakeCrewMember());
            voyage.Tick();

            Assert.That(_state.Phase, Is.EqualTo(VoyagePhase.Briefing));
            Assert.That(_state.Voyage, Is.EqualTo(2));
            Assert.That(_participant.Calls.Count(call => call == "reset"), Is.EqualTo(2));
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
            new(new FakeNetworkService(), _actors, _time, _events, new VoyageRouteProcessor(), _config, new ISessionParticipant[] { _participant },
                new System.Random(7), _loss);
    }
}
