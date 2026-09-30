#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship.Damage;
using TinCan.Features.DesignedEvents;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    public class EventDirectorUseCaseTests
    {
        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private const int TickRate = 10;

        private static readonly EventDefinition Stress = new EventDefinition.Builder(1, "Stress")
            .Phase("Warn", p => p.OnEnter(new Announce("warn")).Duration(1f))
            .Phase("Break", p => p.OnEnter(new BreakShipPart(0), new BreakShipPart(2)).Until(new BrokenPartsAtMost(0)).Timeout(5f))
            .OnSuccess(new Announce("won"))
            .OnFailure(new Announce("lost"))
            .Build();

        private FakeSessionNetworkService _network = null!;
        private FakeTimeService _time = null!;
        private FakeHudValues _hud = null!;
        private FakeShipBreakage _breakage = null!;
        private EventDirectorSettings _settings = null!;

        [SetUp]
        public void SetUp()
        {
            _network = new FakeSessionNetworkService { IsServer = true, IsClient = true };
            _time = new FakeTimeService { TickRate = TickRate, Tick = 0 };
            _hud = new FakeHudValues();
            _breakage = new FakeShipBreakage();
            _settings = new EventDirectorSettings { AutoStart = false, FirstEventDelay = 2f, QuietGap = 3f };
        }

        private EventDirectorUseCase Director(params EventDefinition[] catalog) => new(_network, _time, new RecordingPublisher(),
            new EventHandlerRegistry(
                new IEventActionHandler[] { new AnnounceActionHandler(_hud), new BreakShipPartActionHandler(_breakage) },
                new IEventConditionHandler[] { new BrokenPartsAtMostConditionHandler(_breakage) }),
            new EventRunProcessor(), _settings, catalog);

        private void RunTicks(EventDirectorUseCase director, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                _time.Tick++;
                director.Tick();
            }
        }

        [Test]
        public void TryStart_EntersFirstPhase_AndRunsItsActions()
        {
            var director = Director(Stress);

            Assert.That(director.TryStart(1, out _), Is.True);
            Assert.That(director.Active, Is.SameAs(Stress));
            Assert.That(director.ActivePhase!.Name, Is.EqualTo("Warn"));
            Assert.That(_hud.All[AnnounceActionHandler.HudKey], Is.EqualTo("warn"));
        }

        [Test]
        public void TimedPhase_MovesOnAfterItsTicks_AndRunsTheNextPhasesActions()
        {
            var director = Director(Stress);
            director.TryStart(1, out _);

            RunTicks(director, TickRate - 1);
            Assert.That(director.ActivePhase!.Name, Is.EqualTo("Warn"));
            RunTicks(director, 1);
            Assert.That(director.ActivePhase!.Name, Is.EqualTo("Break"));
            Assert.That(_breakage.Broken, Is.EquivalentTo(new[] { 0, 2 }));
        }

        [Test]
        public void RepairingInTime_Succeeds_AndRunsSuccessActions()
        {
            var director = Director(Stress);
            director.TryStart(1, out _);
            RunTicks(director, TickRate);

            _breakage.TryRestore(0);
            RunTicks(director, 1);
            Assert.That(director.Active, Is.Not.Null, "one part still broken");

            _breakage.TryRestore(2);
            RunTicks(director, 1);
            Assert.That(director.Active, Is.Null);
            Assert.That(director.LastOutcome, Is.EqualTo(EventOutcome.Succeeded));
            Assert.That(director.LastFinished, Is.SameAs(Stress));
            Assert.That(_hud.All[AnnounceActionHandler.HudKey], Is.EqualTo("won"));
        }

        [Test]
        public void NotRepairing_FailsOnTimeout_AndRunsFailureActions()
        {
            var director = Director(Stress);
            director.TryStart(1, out _);

            RunTicks(director, TickRate + 5 * TickRate);
            Assert.That(director.LastOutcome, Is.EqualTo(EventOutcome.Failed));
            Assert.That(_hud.All[AnnounceActionHandler.HudKey], Is.EqualTo("lost"));
        }

        [Test]
        public void TryStart_Refuses_OnClient_WhileRunning_UnknownId_AndMissingHandlers()
        {
            var director = Director(Stress);

            _network.IsServer = false;
            Assert.That(director.TryStart(1, out var reason), Is.False);
            Assert.That(reason, Does.Contain("server only"));

            _network.IsServer = true;
            Assert.That(director.TryStart(99, out reason), Is.False);
            Assert.That(reason, Does.Contain("no event with id 99"));

            director.TryStart(1, out _);
            Assert.That(director.TryStart(1, out reason), Is.False);
            Assert.That(reason, Does.Contain("is running"));

            var bare = new EventDirectorUseCase(_network, _time, new RecordingPublisher(),
                new EventHandlerRegistry(new IEventActionHandler[0], new IEventConditionHandler[0]),
                new EventRunProcessor(), _settings, new[] { Stress });
            Assert.That(bare.TryStart(1, out reason), Is.False);
            Assert.That(reason, Does.Contain("Announce").And.Contain("BrokenPartsAtMost"));
        }

        [Test]
        public void Client_DoesNothing()
        {
            _network.IsServer = false;
            _settings.AutoStart = true;
            var director = Director(Stress);

            RunTicks(director, 10 * TickRate);
            Assert.That(director.Active, Is.Null);
            Assert.That(_breakage.BrokenCount, Is.Zero);
        }

        [Test]
        public void AutoStart_WaitsFirstDelay_ThenQuietGapBetweenEvents()
        {
            _settings.AutoStart = true;
            var director = Director(Stress);

            RunTicks(director, 2 * TickRate);
            Assert.That(director.Active, Is.Null, "first tick sets the clock; delay not over yet");
            RunTicks(director, 1);
            Assert.That(director.Active, Is.SameAs(Stress));

            RunTicks(director, TickRate + 5 * TickRate); // fails on timeout
            Assert.That(director.Active, Is.Null);
            _breakage.TryRestore(0);
            _breakage.TryRestore(2);

            RunTicks(director, 3 * TickRate - 1);
            Assert.That(director.Active, Is.Null, "quiet gap");
            RunTicks(director, 1);
            Assert.That(director.Active, Is.SameAs(Stress));
        }

        [Test]
        public void AutoStart_Off_NeverStarts()
        {
            var director = Director(Stress);
            RunTicks(director, 100 * TickRate);
            Assert.That(director.Active, Is.Null);
        }

        [Test]
        public void Session_StoppingDropsTheRunningEvent_AndTheRotationRunsOnlyWhileActive()
        {
            _settings.AutoStart = true;
            var director = Director(Stress);
            director.TryStart(1, out _);

            director.SetSessionActive(false);
            Assert.That(director.Active, Is.Null, "dropped: nothing breaks during the briefing or on the end screen");
            Assert.That(director.AutoStart, Is.False);
            RunTicks(director, 100 * TickRate);
            Assert.That(director.Active, Is.Null);

            director.ResetForSession();
            director.SetSessionActive(true);
            RunTicks(director, 2 * TickRate);
            Assert.That(director.Active, Is.Null, "the first event waits its delay again");
            RunTicks(director, 1);
            Assert.That(director.Active, Is.SameAs(Stress));
        }
    }
}
