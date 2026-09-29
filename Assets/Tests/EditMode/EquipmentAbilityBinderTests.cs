#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Core.Items;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class EquipmentAbilityBinderTests
    {
        private readonly List<Object> _created = new();
        private FakeActorRegistry _actors = null!;
        private AbilitySystemUseCase _abilities = null!;
        private EquipmentAbilityBinder _binder = null!;
        private FakeAbilityController _player = null!;
        private AbilityDefinition _swing = null!;
        private AbilityDefinition _sprint = null!;
        private GameplayTag _holdingNet = null!;
        private ItemDefinition _net = null!;
        private ItemDefinition _can = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, new FakeTimeService(), new FakeEventPublisher());
            _binder = new EquipmentAbilityBinder(_abilities, _actors);
            _player = new FakeAbilityController();
            _actors.Register(_player);

            _swing = Make<AbilityDefinition>("GA_SwingNet");
            _sprint = Make<AbilityDefinition>("GA_Sprint");
            _holdingNet = Make<GameplayTag>("State.Carrying.Net");
            var holdingEffect = Make<GameplayEffectDefinition>("GE_HoldingNet");
            holdingEffect.DurationType = DurationType.Infinite;
            holdingEffect.Modifiers = new List<AttributeModifier>();
            holdingEffect.GrantedTags = new List<GameplayTag> { _holdingNet };

            _net = Track(ItemDefinition.Create(2, "ITEM_CatchingNet", "Carry_Net", holdingEffect, _swing, _sprint));
            _can = Track(ItemDefinition.Create(1, "ITEM_JerryCan", "Carry_JerryCan"));
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            foreach (var created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void Bind_GrantsItemAbilitiesAndEffectTags()
        {
            _binder.Bind(_player, _net);

            Assert.That(_abilities.HasAbility(_player, _swing), Is.True);
            Assert.That(_player.HasTag(_holdingNet), Is.True);
            Assert.That(_binder.BoundItem(_player.Id), Is.SameAs(_net));
        }

        [Test]
        public void Unbind_RevokesExactlyWhatWasGranted()
        {
            _binder.Bind(_player, _net);

            _binder.Bind(_player, null);

            Assert.That(_abilities.HasAbility(_player, _swing), Is.False);
            Assert.That(_player.HasTag(_holdingNet), Is.False);
            Assert.That(_binder.BoundItem(_player.Id), Is.Null);
        }

        [Test]
        public void Unbind_KeepsAbilitiesTheActorAlreadyHad()
        {
            _abilities.GrantAbility(_player, _sprint);
            _binder.Bind(_player, _net);

            _binder.Bind(_player, null);

            Assert.That(_abilities.HasAbility(_player, _sprint), Is.True, "Starting abilities must survive unequipping.");
            Assert.That(_abilities.HasAbility(_player, _swing), Is.False);
        }

        [Test]
        public void Swap_RevokesTheOldItemBeforeGrantingTheNew()
        {
            _binder.Bind(_player, _net);

            _binder.Bind(_player, _can);

            Assert.That(_abilities.HasAbility(_player, _swing), Is.False);
            Assert.That(_player.HasTag(_holdingNet), Is.False);
            Assert.That(_binder.BoundItem(_player.Id), Is.SameAs(_can));
        }

        [Test]
        public void Rebinding_TheSameItem_DoesNotStackEffects()
        {
            _binder.Bind(_player, _net);
            _binder.Bind(_player, _net);

            _binder.Bind(_player, null);

            Assert.That(_player.HasTag(_holdingNet), Is.False, "A second apply would leave a tag behind after one revoke.");
        }

        [Test]
        public void Despawn_ForgetsTheActor()
        {
            _binder.Bind(_player, _net);

            _actors.Unregister(_player);

            Assert.That(_binder.BoundItem(_player.Id), Is.Null);
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            var created = ScriptableObject.CreateInstance<T>();
            created.name = name;
            _created.Add(created);
            return created;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
