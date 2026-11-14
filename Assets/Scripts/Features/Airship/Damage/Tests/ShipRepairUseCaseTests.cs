#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Core.Targeting;
using TinCan.Features.SkillChecks;
using UnityEngine;
using TinCan.Tests.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Features.Airship.Damage.Tests
{
    public class ShipRepairUseCaseTests
    {
        private sealed class RecordingPublisher : TinCan.Core.Domain.Events.IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        /// <summary>The checks the repair opens: which host, for whom, and where the grade's effect goes.</summary>
        private sealed class RecordingChecks : ISkillChecks
        {
            public List<(ISkillCheckHost Host, ulong Client, IAbilityControllerBase? Target)> Opened { get; } = new();
            public bool TryOpen(ISkillCheckHost host, ulong clientId, IAbilityControllerBase? target = null)
            {
                Opened.Add((host, clientId, target));
                return true;
            }
            public int Count(SkillCheckGrade grade) => 0;
        }

        /// <summary>The repair check beside a part, as the prefab has it (the real one is a NetworkBehaviour).</summary>
        private sealed class RepairHost : MonoBehaviour, ISkillCheckHost
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public IAirshipView? Ship => null;
            public SkillCheckDefinition? Definition => null;
            public void ServerOpen(ulong clientId, int checkId, int level) { }
            public bool TryTakeOpened(out int checkId, out int level) { checkId = level = 0; return false; }
            public void SendGrade(int checkId, SkillCheckGrade grade) { }
            public IReadOnlyList<SkillCheckResult> TakeResults() => Array.Empty<SkillCheckResult>();
            public void ServerShowLit(string? challenge, int secondsLeft) { }
            public string? LitChallenge => null;
            public int LitSecondsLeft => 0;
        }

        private readonly List<Object> _assets = new();
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private RecordingPublisher _events = null!;
        private AbilitySystemUseCase _abilities = null!;
        private ShipDamageConfig _config = null!;
        private GameplayTag _repairing = null!;
        private FakeAirshipView _airship = null!;
        private FakeShipDamagePoint[] _points = null!;
        private RepairHost[] _hosts = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeNetHumanoidView _player = null!;
        private RecordingChecks _checks = null!;
        private ShipRepairUseCase _useCase = null!;
        private TargetableRegistry _targetables = null!;
        private GameplayTag _damaged = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService { DeltaTime = 0.25f };
            _events = new RecordingPublisher();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);

            var health = Create<HealthAttribute>("Attr_Health");
            var maxHealth = Create<MaxHealthAttribute>("Attr_MaxHealth");
            _repairing = Create<GameplayTag>("State.Repairing");

            _config = Create<ShipDamageConfig>("ShipDamageConfig");
            _config.RepairingTag = _repairing;
            _damaged = Create<GameplayTag>("State.Damaged");
            var scan = Create<TargetingDefinition>("TD_RepairScan");
            scan.Source = AimSource.BodyForward;
            scan.Shape = TargetShape.Cone;
            scan.Range = 2.5f;
            scan.HorizontalAngle = 100f;
            scan.VerticalAngle = 120f;
            scan.RequiredTags = new List<GameplayTag> { _damaged };
            scan.Selection = TargetSelection.Nearest;
            var ability = Create<AbilityDefinition>("GA_RepairShip");
            ability.Targeting = scan;
            _config.RepairAbility = ability;

            _airship = new FakeAirshipView("Airship");
            _points = FakeShipDamage.AttachPoints(_airship.GameObject, 2, health, maxHealth);
            _points[0].transform.position = new Vector3(0f, 1f, 2f);
            _points[1].transform.position = new Vector3(0f, 1f, -2f);
            _hosts = new[] { _points[0].gameObject.AddComponent<RepairHost>(), _points[1].gameObject.AddComponent<RepairHost>() };
            _targetables = new TargetableRegistry();
            foreach (var point in _points) _targetables.Register(point);
            _actors.Register(_airship);

            _movement = new FakeHumanoidMovementView("Player");
            _movement.Transform.position = Vector3.zero;
            _movement.Transform.rotation = Quaternion.identity;
            _player = new FakeNetHumanoidView(_movement) { OwnerId = 7 };
            _actors.Register(_player);

            _checks = new RecordingChecks();
            _useCase = UseCase(new FakeNetworkService());
        }

        [TearDown]
        public void TearDown()
        {
            _airship.Destroy();
            _movement.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void APull_OpensThePartsCheck_ForThePlayer_WithThePartAsTheTarget()
        {
            Break(_points[0], 0f);
            _player.AddTag(_repairing);

            _useCase.Tick();

            Assert.That(_checks.Opened, Has.Count.EqualTo(1));
            Assert.That(_checks.Opened[0].Host, Is.SameAs(_hosts[0]));
            Assert.That(_checks.Opened[0].Client, Is.EqualTo(7UL));
            Assert.That(_checks.Opened[0].Target, Is.SameAs(_points[0].Controller), "The grade heals the part, not the ship.");
        }

        [Test]
        public void HoldingOn_OpensOneCheck_AndANewPullOpensTheNext()
        {
            Break(_points[0], 0f);
            _player.AddTag(_repairing);

            for (int i = 0; i < 5; i++) _useCase.Tick();
            Assert.That(_checks.Opened, Has.Count.EqualTo(1), "One check per pull.");

            _player.RemoveTag(_repairing);
            _useCase.Tick();
            _player.AddTag(_repairing);
            _useCase.Tick();
            Assert.That(_checks.Opened, Has.Count.EqualTo(2));
        }

        [Test]
        public void APullOffTarget_OpensTheCheck_OnceThePlayerTurnsToTheBrokenPart()
        {
            Break(_points[0], 0f);
            _movement.Transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _player.AddTag(_repairing);

            _useCase.Tick();
            Assert.That(_checks.Opened, Is.Empty, "Facing a healthy part.");

            _movement.Transform.rotation = Quaternion.identity;
            _useCase.Tick();
            Assert.That(_checks.Opened, Has.Count.EqualTo(1));
        }

        [Test]
        public void NotRepairing_OpensNothing()
        {
            Break(_points[0], 0f);

            _useCase.Tick();

            Assert.That(_checks.Opened, Is.Empty);
        }

        [Test]
        public void PartWithoutTheDamagedTag_IsNotTargeted()
        {
            _points[0].SetHealthFraction(0f);
            _player.AddTag(_repairing);

            _useCase.Tick();

            Assert.That(_checks.Opened, Is.Empty, "TD_RepairScan requires State.Damaged on the target.");
        }

        [Test]
        public void OnAClient_PredictsTheLocalPlayersTarget_WithoutOpening()
        {
            Break(_points[0], 0f);
            _player.AddTag(_repairing);
            _actors.LocalPlayer = _player;
            var client = UseCase(new ClientNetwork());

            client.Tick();

            Assert.That(client.PredictedTarget, Is.SameAs(_points[0]));
            Assert.That(_checks.Opened, Is.Empty, "Only the server opens checks.");

            _player.RemoveTag(_repairing);
            client.Tick();
            Assert.That(client.PredictedTarget, Is.Null);
        }

        [Test]
        public void HoldingTheTool_AimsAtTheLocalPlayersTarget_BeforeTheTriggerIsPulled()
        {
            Break(_points[0], 0f);
            _actors.LocalPlayer = _player;

            _useCase.Tick();
            Assert.That(_useCase.AimedTarget, Is.Null, "Empty hands aim at nothing.");

            _abilities.GrantAbility(_player, _config.RepairAbility!);
            _useCase.Tick();
            Assert.That(_useCase.AimedTarget, Is.SameAs(_points[0]), "The part in front, with no repairing tag.");
            Assert.That(_useCase.PredictedTarget, Is.Null, "Not repairing, and the host predicts nothing.");
            Assert.That(_checks.Opened, Is.Empty, "Aiming opens nothing.");

            _movement.Transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _useCase.Tick();
            Assert.That(_useCase.AimedTarget, Is.Null, "Turned toward a part that is not broken: nothing to aim at.");
        }

        private ShipRepairUseCase UseCase(TinCan.Core.Domain.Networking.INetworkService network) =>
            new(network, _actors, _events, _abilities, new TargetingUseCase(_targetables, new TargetingProcessor()), _checks, _config);

        private sealed class ClientNetwork : TinCan.Core.Domain.Networking.INetworkService
        {
            public TinCan.Core.Domain.Networking.NetworkState State => TinCan.Core.Domain.Networking.NetworkState.Client;
            public bool IsActive => true;
            public bool IsServer => false;
            public bool IsClient => true;
            public bool IsHost => false;
            public ulong LocalClientId => 1;
            public void SetPlayerPrefab(GameObject prefab) { }
            public void SetConnection(string address, ushort port) { }
            public void SetListenEndpoint(string listenAddress, ushort port) { }
            public void StartHost() { }
            public void StartServer() { }
            public void StartClient() { }
            public void Shutdown() { }
        }

        // In the game the breakage reconcile grants State.Damaged to a broken part; the repair scan filters on it.
        private void Break(FakeShipDamagePoint point, float fraction)
        {
            point.SetHealthFraction(fraction);
            point.FakeController.AddTag(_damaged);
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }
}
