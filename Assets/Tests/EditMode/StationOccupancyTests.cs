#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Features.Stations;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Occupying a station while staying in the body: <see cref="StationOccupancyUseCase"/> and
    /// <see cref="OccupyStationInteractionHandler"/> (plan <c>cannon-and-hazards.md</c>, S1 occupancy).
    /// </summary>
    public class StationOccupancyTests
    {
        private sealed class FakeStation : IStation
        {
            private readonly GameObject _seat = new("Seat");

            public Transform? Seat => _seat.transform;
            public AbilityDefinition? OccupyAbility { get; set; }
            public List<AbilityDefinition> Granted { get; } = new();
            public IReadOnlyList<AbilityDefinition> GrantedAbilities => Granted;
            public ulong? OccupantClientId { get; private set; }
            public Camera? ViewCamera => null;
            public IHumanoidCharacterView? Occupant { get; private set; }
            public InteractionDefinition Definition => null!;

            public void ServerSetOccupant(IHumanoidCharacterView? occupant)
            {
                Occupant = occupant;
                OccupantClientId = occupant != null ? 1UL : null;
            }

            public void Destroy() => Object.DestroyImmediate(_seat);
        }

        private sealed class FakeRespawn : IHumanoidRespawnService
        {
            public List<(IHumanoidCharacterView Character, Vector3 Position)> Resets { get; } = new();

            public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation)
            {
                Resets.Add((character, position));
                character.Movement.SetPose(position, rotation);
            }
        }

        private readonly List<Object> _assets = new();
        private readonly List<FakeHumanoidMovementView> _bodies = new();
        private FakeActorRegistry _actors = null!;
        private AbilitySystemUseCase _abilities = null!;
        private FakeRespawn _respawn = null!;
        private StationOccupancyUseCase _occupancy = null!;
        private FakeStation _station = null!;
        private AbilityDefinition _occupy = null!;
        private AbilityDefinition _fire = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, new FakeTimeService(), new FakeEventPublisher());
            _respawn = new FakeRespawn();
            _occupancy = new StationOccupancyUseCase(new FakeNetworkService(), _actors, _abilities, _respawn, new FakeEventPublisher());
            _occupy = Ability("GA_OccupyCannon");
            _fire = Ability("GA_FireCannon");
            _station = new FakeStation { OccupyAbility = _occupy };
            _station.Granted.Add(_fire);
            _station.Seat!.position = new Vector3(3f, 1f, 2f);
        }

        [TearDown]
        public void TearDown()
        {
            _station.Destroy();
            foreach (var body in _bodies) body.Destroy();
            _bodies.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Occupy_RunsTheOccupyAbility_GrantsTheStationsAbilities_AndSeatsThePlayer()
        {
            var player = Player();

            Assert.That(_occupancy.TryOccupy(player, _station), Is.True);

            Assert.That(_abilities.TryActivateAbility(player, _occupy), Is.False, "the occupy ability is running");
            Assert.That(_abilities.HasAbility(player, _fire), Is.True);
            Assert.That(_respawn.Resets, Has.Count.EqualTo(1));
            Assert.That(Vector3.Distance(player.Movement.Transform.position, _station.Seat!.position), Is.LessThan(1e-4f));
            Assert.That(_station.Occupant, Is.SameAs(player));
            Assert.That(_occupancy.StationOf(player.Id), Is.SameAs(_station));
            Assert.That(_occupancy.OccupantOf(_station), Is.SameAs(player));
        }

        [Test]
        public void Occupy_IsExclusive()
        {
            var first = Player();
            var second = Player();
            _occupancy.TryOccupy(first, _station);

            Assert.That(_occupancy.TryOccupy(second, _station), Is.False);
            Assert.That(_abilities.HasAbility(second, _fire), Is.False);
            Assert.That(_station.Occupant, Is.SameAs(first));
        }

        [Test]
        public void Occupy_RefusedWhenTheOccupyAbilityCannotActivate()
        {
            var player = Player();
            var cooldown = Create<GameplayEffectDefinition>("GE_Cooldown");
            cooldown.DurationType = DurationType.Duration;
            cooldown.DurationSeconds = 10f;
            cooldown.Modifiers = new List<AttributeModifier>();
            cooldown.GrantedTags = new List<GameplayTag>();
            _occupy.CooldownEffect = cooldown;
            _occupancy.TryOccupy(player, _station);
            _occupancy.Leave(player.Id);

            Assert.That(_occupancy.TryOccupy(player, _station), Is.False, "the occupy ability is on cooldown");
            Assert.That(_station.Occupant, Is.Null);
            Assert.That(_abilities.HasAbility(player, _fire), Is.False);
        }

        [Test]
        public void Leave_UndoesExactlyWhatOccupyDid()
        {
            var player = Player();
            _occupancy.TryOccupy(player, _station);

            Assert.That(_occupancy.Leave(player.Id), Is.True);

            Assert.That(_abilities.HasAbility(player, _fire), Is.False);
            Assert.That(_abilities.TryActivateAbility(player, _occupy), Is.True, "the occupy ability ended");
            Assert.That(_station.Occupant, Is.Null);
            Assert.That(_occupancy.StationOf(player.Id), Is.Null);
            Assert.That(_occupancy.Leave(player.Id), Is.False, "nothing left to leave");
        }

        [Test]
        public void Leave_KeepsAbilitiesThePlayerHadBefore()
        {
            var player = Player();
            _abilities.GrantAbility(player, _fire);
            _occupancy.TryOccupy(player, _station);

            _occupancy.Leave(player.Id);

            Assert.That(_abilities.HasAbility(player, _fire), Is.True, "the station did not grant it, so it does not take it");
        }

        [Test]
        public void InteractWhileOccupying_Leaves_OtherwiseFallsThrough()
        {
            var player = Player();
            Assert.That(_occupancy.TryHandleInteract(player), Is.False, "not occupying: Interact means interact");

            _occupancy.TryOccupy(player, _station);

            Assert.That(_occupancy.TryHandleInteract(player), Is.True);
            Assert.That(_station.Occupant, Is.Null);
        }

        [Test]
        public void Tick_ReleasesTheStation_WhenTheOccupantDespawns()
        {
            var player = Player();
            _occupancy.TryOccupy(player, _station);

            _actors.Unregister(player);
            _occupancy.Tick();

            Assert.That(_station.Occupant, Is.Null);
            Assert.That(_occupancy.OccupantOf(_station), Is.Null);
        }

        [Test]
        public void Handler_OccupiesTheTargetStation()
        {
            var player = Player();
            var handler = new OccupyStationInteractionHandler(_occupancy);

            handler.Handle(new InteractionContext(player, _station, null!));

            Assert.That(_station.Occupant, Is.SameAs(player));
        }

        private FakeHumanoidCharacterView Player()
        {
            var body = new FakeHumanoidMovementView("Player");
            _bodies.Add(body);
            var player = new FakeHumanoidCharacterView(body);
            _actors.Register(player);
            return player;
        }

        private AbilityDefinition Ability(string name)
        {
            var ability = Create<AbilityDefinition>(name);
            ability.CancelAbilitiesWithTag = new List<GameplayTag>();
            ability.BlockAbilitiesWithTag = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnActor = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnActor = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnTarget = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnTarget = new List<GameplayTag>();
            ability.TimingTagWindows = new List<AbilityTagWindow>();
            return ability;
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
