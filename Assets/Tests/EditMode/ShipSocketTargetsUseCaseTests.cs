#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Targeting;
using TinCan.Core.Interaction;
using TinCan.Core.Ship.Sockets;
using TinCan.Core.UI;
using TinCan.Features.ShipSockets;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipSocketTargetsUseCase"/> (free sockets are things to press E on), <see cref="MountFittingInteractionHandler"/>
    /// (E asks the player to choose) and <see cref="ShipFittingMenuUseCase"/> with <see cref="MountFittingMenuCommand"/>
    /// (the choice goes back as a request).
    /// </summary>
    public class ShipSocketTargetsUseCaseTests
    {
        private const string Cannon = "weapon.cannon";

        private FakeActorRegistry _actors = null!;
        private FakeAirshipView _ship = null!;
        private FakeShipFittingState _state = null!;
        private FakeShipSocketList _sockets = null!;
        private FakeTargetableRegistry _targetables = null!;
        private ShipSocketsConfig _config = null!;
        private ShipSocketTargetsUseCase _targets = null!;
        private ShipSocketInfo _bow;
        private ShipFittingCatalog _catalog = null!;
        private readonly List<Object> _objects = new();

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _ship = new FakeAirshipView("Ship");
            _state = new FakeShipFittingState(_ship);
            _actors.Register(_ship);
            _actors.Register(_state);
            _sockets = new FakeShipSocketList();
            _bow = FakeShipSockets.Socket(_ship.Transform, 7, new Vector3(1f, -0.5f, 3f));
            _sockets.Ships[_ship.Id] = new List<ShipSocketInfo> { _bow };
            _targetables = new FakeTargetableRegistry();
            _config = Keep(ScriptableObject.CreateInstance<ShipSocketsConfig>());
            _config.MountInteraction = Keep(ScriptableObject.CreateInstance<InteractionDefinition>());
            _targets = new ShipSocketTargetsUseCase(_actors, _sockets, _targetables, _config);
            _catalog = new ShipFittingCatalog(new[] { Keep(ShipFittingDefinition.Create(Cannon, Keep(new GameObject("CannonStation")), "Cannon station")) });
        }

        [TearDown]
        public void TearDown()
        {
            _targets.Dispose();
            _ship.Destroy();
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        private T Keep<T>(T o) where T : Object
        {
            _objects.Add(o);
            return o;
        }

        [Test]
        public void EachSocket_GetsATarget_AtTheSocket_RegisteredForTargeting()
        {
            _targets.Tick();

            var target = _targets.Targets.Single();
            Assert.That(target.transform.parent, Is.SameAs(_bow.Mount));
            Assert.That((target.ShipId, target.Socket), Is.EqualTo((_ship.Id, _bow.Id)));
            Assert.That(_targetables.All, Is.EqualTo(new ITargetable[] { target }));
            Assert.That(((ITargetable)target).IsTargetable, Is.True);
            Assert.That(target.GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test]
        public void ATakenSocket_IsNotTargetable_AndItsTriggerIsOff()
        {
            _targets.Tick();
            _state.ServerSetMounted(new[] { new MountedFitting(_bow.Id, Cannon, 1) });

            _targets.Tick();

            var target = _targets.Targets.Single();
            Assert.That(((ITargetable)target).IsTargetable, Is.False);
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
        }

        [Test]
        public void ASocketThatGoes_LosesItsTarget()
        {
            _targets.Tick();

            _sockets.Ships[_ship.Id].Clear();
            _targets.Tick();

            Assert.That(_targets.Targets, Is.Empty);
            Assert.That(_targetables.All, Is.Empty);
        }

        [Test]
        public void E_OnAFreeSocket_AsksThePlayerWhoPressedIt_ToChoose()
        {
            _targets.Tick();
            var target = _targets.Targets.Single();
            var player = Keep(FakeShipSockets.Player(Vector3.zero, 5).gameObject).GetComponent<FakeFittingPlayer>();
            var handler = new MountFittingInteractionHandler(_actors, _catalog, new FakeEventPublisher());

            handler.Handle(new InteractionContext(player, target, target.Definition));

            Assert.That(_state.ChoosersOpened, Is.EqualTo(new[] { (5ul, _bow.Id) }));

            target.IsFree = false;
            handler.Handle(new InteractionContext(player, target, target.Definition));
            Assert.That(_state.ChoosersOpened.Count, Is.EqualTo(1), "a taken socket opens nothing");
        }

        [Test]
        public void TheChooser_OpensTheFittingMenu_AndARow_SendsTheMountRequest()
        {
            var choice = new ShipFittingChoice();
            var command = new MountFittingMenuCommand(choice);
            var menus = new MenuUseCase(new MenuCommandRegistry(new IMenuCommand[] { command }));
            var menu = new ShipFittingMenuUseCase(_actors, _catalog, menus, choice);

            menu.Tick();
            Assert.That(menus.IsOpen, Is.False, "nothing asked yet");

            _state.ServerOpenChooser(5, _bow.Id);
            menu.Tick();
            Assert.That(menus.Current!.MenuId, Is.EqualTo(ShipFittingMenuUseCase.MenuId));
            Assert.That(menus.Current.Items.Select(i => i.Label), Is.EqualTo(new[] { "Cannon station", "Leave it empty" }));

            menus.Invoke(Cannon);

            Assert.That(_state.Sent, Is.EqualTo(new[] { (_bow.Id, Cannon) }));
            Assert.That(menus.IsOpen, Is.False);
        }
    }
}
