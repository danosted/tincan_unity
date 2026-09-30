#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Core.Humanoid;
using TinCan.Features.Boarding;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="BoardingUseCase"/>. Plan: <c>crew-gate-and-boarding.md</c>.</summary>
    public class BoardingUseCaseTests
    {
        private sealed class RecordingRespawnService : IHumanoidRespawnService
        {
            public List<(IHumanoidCharacterView Character, Vector3 Position)> Resets { get; } = new();

            public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation)
            {
                Resets.Add((character, position));
                character.Movement.SetPose(position, rotation);
            }
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private FakeActorRegistry _actors = null!;
        private FakeSessionNetworkService _network = null!;
        private RecordingRespawnService _respawn = null!;
        private RecordingPublisher _events = null!;
        private BoardingConfig _config = null!;
        private FakeAirshipView _ship = null!;
        private readonly List<FakeHumanoidMovementView> _bodies = new();

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _network = new FakeSessionNetworkService { IsServer = true };
            _respawn = new RecordingRespawnService();
            _events = new RecordingPublisher();
            _config = ScriptableObject.CreateInstance<BoardingConfig>();
            _config.BoardingOffset = new Vector3(0f, 3f, 0f);
            _ship = new FakeAirshipView("Airship");
            _ship.Transform.position = new Vector3(0f, 100f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            foreach (var body in _bodies) Object.DestroyImmediate(body.Transform.gameObject);
            _bodies.Clear();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void ANewPlayer_WithAShip_IsPlacedAtTheBoardingPose_Once()
        {
            _actors.Register(_ship);
            var player = Player();
            var boarding = UseCase();

            boarding.Tick();
            boarding.Tick();

            Assert.That(_respawn.Resets.Count, Is.EqualTo(1));
            Assert.That(Vector3.Distance(_respawn.Resets[0].Position, new Vector3(0f, 103f, 0f)), Is.LessThan(1e-3f));
            Assert.That(_events.Events.OfType<PlayerBoardedEvent>().Single().CharacterId, Is.EqualTo(player.Id));
        }

        [Test]
        public void ALateJoiner_IsBoarded_AndTheCrewAlreadyAboardIsLeftAlone()
        {
            _actors.Register(_ship);
            var first = Player();
            var boarding = UseCase();
            boarding.Tick();

            var late = Player();
            boarding.Tick();

            Assert.That(_respawn.Resets.Select(reset => reset.Character), Is.EqualTo(new[] { first, late }));
        }

        [Test]
        public void APlayerSeenBeforeAnyShip_StaysWhereItSpawned_AndIsNotRevisited()
        {
            Player();
            var boarding = UseCase();
            boarding.Tick();

            _actors.Register(_ship);
            boarding.Tick();

            Assert.That(_respawn.Resets, Is.Empty);
        }

        [Test]
        public void ABodyNobodyPlays_IsNotBoarded()
        {
            _actors.Register(_ship);
            Player().IsPlayerCharacter = false;
            var boarding = UseCase();

            boarding.Tick();

            Assert.That(_respawn.Resets, Is.Empty);
        }

        [Test]
        public void OnAClient_DoesNothing()
        {
            _network.IsServer = false;
            _actors.Register(_ship);
            Player();

            UseCase().Tick();

            Assert.That(_respawn.Resets, Is.Empty);
        }

        [Test]
        public void APlayerWhoLeftAndCameBack_IsBoardedAgain()
        {
            _actors.Register(_ship);
            var player = Player();
            var boarding = UseCase();
            boarding.Tick();

            _actors.Unregister(player);
            boarding.Tick();
            _actors.Register(player);
            boarding.Tick();

            Assert.That(_respawn.Resets.Count, Is.EqualTo(2));
        }

        private FakeHumanoidCharacterView Player()
        {
            var body = new FakeHumanoidMovementView("Player");
            body.Transform.position = new Vector3(0f, 40f, 0f);
            _bodies.Add(body);
            var player = new FakeHumanoidCharacterView(body);
            _actors.Register(player);
            return player;
        }

        private BoardingUseCase UseCase() => new(_network, _actors, _respawn, _events, _config);
    }
}
