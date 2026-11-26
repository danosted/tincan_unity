#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Ship;
using TinCan.Core.UI;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.ShipLoss.Tests
{
    /// <summary>
    /// <see cref="ShipLossUseCase"/> (lost, held, brought back), <see cref="RespawnShipMenuCommand"/> and
    /// <see cref="ShipLostScreenPresenter"/>. Plan: <c>ship-lost-and-respawn.md</c>.
    /// </summary>
    public class ShipLossUseCaseTests
    {
        private sealed class FakeLossState : IShipLossState
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public bool IsLost { get; private set; }
            public bool RespawnRequested;

            public void RequestRespawn() => RespawnRequested = true;

            public bool ConsumeRespawnRequest()
            {
                bool requested = RespawnRequested;
                RespawnRequested = false;
                return requested;
            }

            public void ServerSetLost(bool lost) => IsLost = lost;
        }

        private sealed class FakeLoss : IShipLossSource, IShipRecovery
        {
            public bool Lost;
            public Vector3? Safe;
            public int Recovered;

            public bool IsLost(IAirshipView ship) => Lost;

            public bool TryGetSafePose(IAirshipView ship, out Vector3 position, out Quaternion rotation)
            {
                position = Safe ?? Vector3.zero;
                rotation = Quaternion.Euler(0f, 90f, 0f);
                return Safe.HasValue;
            }

            public void Recover(IAirshipView ship)
            {
                Recovered++;
                Lost = false;
            }
        }

        private sealed class RecordingRespawn : IHumanoidRespawnService
        {
            public List<IHumanoidCharacterView> Resets { get; } = new();

            public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation) => Resets.Add(character);
        }

        private sealed class NullPublisher : IEventPublisher
        {
            public void Publish<TEvent>(TEvent evt) { }
        }

        private FakeActorRegistry _actors = null!;
        private FakeLossState _state = null!;
        private FakeLoss _loss = null!;
        private RecordingRespawn _respawn = null!;
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _ship = null!;
        private HealthAttribute _health = null!;
        private MaxHealthAttribute _maxHealth = null!;
        private ShipLossUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _state = new FakeLossState();
            _loss = new FakeLoss();
            _respawn = new RecordingRespawn();

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
            _useCase = new ShipLossUseCase(new FakeNetworkService(), _actors, new NullPublisher(),
                new IShipLossSource[] { _loss }, new IShipRecovery[] { _loss }, _respawn);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            UnityEngine.Object.DestroyImmediate(_health);
            UnityEngine.Object.DestroyImmediate(_maxHealth);
        }

        [Test]
        public void AWholeShip_IsNotLost_NorHeld()
        {
            _useCase.Tick();

            Assert.That(_useCase.IsLost, Is.False);
            Assert.That(_useCase.Holds(_ship), Is.False);
        }

        [Test]
        public void TheShipsHealthRunningOut_LosesIt_AndHoldsItStill()
        {
            _shipController.SetAttribute(_health, new AttributeValue(0f));
            _useCase.Tick();

            Assert.That(_state.IsLost, Is.True);
            Assert.That(_useCase.Holds(_ship), Is.True, "the lost ship stays where it went down");
        }

        [Test]
        public void ALossSourceLosingTheShip_LosesIt()
        {
            _loss.Lost = true;
            _useCase.Tick();

            Assert.That(_useCase.IsLost, Is.True);
        }

        [Test]
        public void Respawn_BringsTheShipBackWholeAtItsSafeSpot_TheCrewAboard()
        {
            var movement = new FakeHumanoidMovementView("Player");
            var player = new FakeHumanoidCharacterView(movement);
            _actors.Register(player);
            _useCase.Tick();
            _loss.Safe = new Vector3(30f, 12f, 200f);
            _ship.Transform.position = new Vector3(30f, -80f, 260f);
            _loss.Lost = true;
            _useCase.Tick();

            _state.RequestRespawn();
            _useCase.Tick();

            Assert.That(_useCase.IsLost, Is.False);
            Assert.That(_ship.Transform.position, Is.EqualTo(new Vector3(30f, 12f, 200f)));
            Assert.That(_loss.Recovered, Is.EqualTo(1), "the ship is made whole");
            Assert.That(_respawn.Resets, Is.EqualTo(new[] { player }), "the crew is put back aboard");
            Assert.That(_useCase.Holds(_ship), Is.False);
            movement.Destroy();
        }

        [Test]
        public void Respawn_WithNoSafeSpot_IsWhereTheShipWasFirstSeen()
        {
            _ship.Transform.position = new Vector3(5f, 20f, 5f);
            _useCase.Tick();
            _ship.Transform.position = new Vector3(5f, -80f, 400f);
            _loss.Lost = true;
            _useCase.Tick();

            _state.RequestRespawn();
            _useCase.Tick();

            Assert.That(_ship.Transform.position, Is.EqualTo(new Vector3(5f, 20f, 5f)));
        }

        [Test]
        public void Respawn_WhileNotLost_DoesNothing()
        {
            _ship.Transform.position = new Vector3(0f, 0f, 300f);

            _state.RequestRespawn();
            _useCase.Tick();

            Assert.That(_ship.Transform.position, Is.EqualTo(new Vector3(0f, 0f, 300f)));
            Assert.That(_loss.Recovered, Is.Zero);
        }

        [Test]
        public void ALostShip_StaysLost_WithoutARespawn()
        {
            _loss.Lost = true;
            _useCase.Tick();
            _loss.Lost = false;

            for (int i = 0; i < 5; i++) _useCase.Tick();

            Assert.That(_useCase.IsLost, Is.True);
        }

        [Test]
        public void BringBack_PlacesTheShipThere_AndClearsTheLoss()
        {
            _loss.Lost = true;
            _useCase.Tick();

            _useCase.BringBack(_ship, new Vector3(1f, 2f, 3f), Quaternion.identity);

            Assert.That(_ship.Transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(_useCase.IsLost, Is.False);
            Assert.That(_loss.Recovered, Is.EqualTo(1));
        }

        [Test]
        public void RespawnMenuCommand_AsksTheStateForARespawn()
        {
            var command = new RespawnShipMenuCommand(_actors);

            command.Execute(new MenuContext(null!, ShipLossMenus.MenuId, ShipLossMenus.RespawnItem));

            Assert.That(_state.RespawnRequested, Is.True);
            Assert.That(command.CommandId, Is.EqualTo(RespawnShipMenuCommand.Id));
        }

        [Test]
        public void LostScreen_IsAPinnedModal_WhileLost_AndGoesWhenTheShipIsBack()
        {
            var menu = MenuDefinition.Create(ShipLossMenus.MenuId, "Lost");
            var menus = new MenuUseCase(new MenuCommandRegistry(Array.Empty<IMenuCommand>()));
            var presenter = new ShipLostScreenPresenter(_useCase, menus, menu);

            presenter.Tick();
            Assert.That(menus.IsOpen, Is.False);

            _loss.Lost = true;
            _useCase.Tick();
            presenter.Tick();
            Assert.That(menus.Current?.MenuId, Is.EqualTo(ShipLossMenus.MenuId));
            Assert.That((menus.Current!.Kind, menus.Current.Pinned), Is.EqualTo((MenuKind.Modal, true)));

            _state.RequestRespawn();
            _useCase.Tick();
            presenter.Tick();
            Assert.That(menus.IsOpen, Is.False);

            UnityEngine.Object.DestroyImmediate(menu);
        }
    }
}
