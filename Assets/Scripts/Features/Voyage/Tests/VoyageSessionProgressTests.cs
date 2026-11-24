#nullable enable
using System;
using NUnit.Framework;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Voyage.Tests
{
    /// <summary><see cref="VoyageSessionProgress"/>: the voyage's progress for the day: none idle, 0 briefing, forward only
    /// under way, 1 arrived, a fresh start each voyage.</summary>
    public class VoyageSessionProgressTests
    {
        private sealed class FakeState : IVoyageState
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public VoyagePhase Phase { get; set; }
            public int Voyage { get; set; } = 1;
            public Vector3 Destination { get; set; } = new(0f, 0f, 1000f);
            public int LayoutSeed => 1;
            public Vector3 Origin => Vector3.zero;
            public int BriefingSecondsLeft => 0;
            public void RequestRestart() { }
            public bool ConsumeRestartRequest() => false;
            public void RequestRespawn() { }
            public bool ConsumeRespawnRequest() => false;
            public void ServerSetPhase(VoyagePhase phase) => Phase = phase;
            public void ServerSetVoyage(int voyage) => Voyage = voyage;
            public void ServerSetDestination(Vector3 destination) => Destination = destination;
            public void ServerSetLayout(int layoutSeed, Vector3 origin) { }
            public void ServerSetBriefingSecondsLeft(int seconds) { }
        }

        private FakeActorRegistry _actors = null!;
        private FakeState _state = null!;
        private FakeAirshipView _ship = null!;
        private VoyageConfig _config = null!;
        private VoyageSessionProgress _progress = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _state = new FakeState();
            _ship = new FakeAirshipView();
            _config = ScriptableObject.CreateInstance<VoyageConfig>();
            _config.RouteLength = 1000f;
            _actors.Register(_state);
            _actors.Register(_ship);
            _progress = new VoyageSessionProgress(_actors, new VoyageRouteProcessor(), _config);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        private float Progress()
        {
            Assert.That(_progress.TryGetProgress(out float progress), Is.True, "a voyage is on");
            return progress;
        }

        [Test]
        public void Idle_HasNoProgress()
        {
            _state.Phase = VoyagePhase.Idle;

            Assert.That(_progress.TryGetProgress(out _), Is.False);
        }

        [Test]
        public void UnderWay_FollowsTheShip_AndNeverGoesBack()
        {
            _state.Phase = VoyagePhase.Underway;
            _ship.Transform.position = new Vector3(0f, 0f, 400f);
            Assert.That(Progress(), Is.EqualTo(0.4f).Within(1e-4f));

            _ship.Transform.position = new Vector3(0f, 0f, 100f); // turned back

            Assert.That(Progress(), Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void Briefing_IsTheStart_AndArrivalTheEnd()
        {
            _state.Phase = VoyagePhase.Briefing;
            Assert.That(Progress(), Is.EqualTo(0f));

            _state.Phase = VoyagePhase.Arrived;
            Assert.That(Progress(), Is.EqualTo(1f));
        }

        [Test]
        public void ANewVoyage_StartsAgain()
        {
            _state.Phase = VoyagePhase.Underway;
            _ship.Transform.position = new Vector3(0f, 0f, 800f);
            Assert.That(Progress(), Is.EqualTo(0.8f).Within(1e-4f));

            _state.Voyage = 2;
            _ship.Transform.position = new Vector3(0f, 0f, 100f);

            Assert.That(Progress(), Is.EqualTo(0.1f).Within(1e-4f));
        }
    }
}
