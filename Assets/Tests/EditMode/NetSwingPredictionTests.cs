#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using TinCan.Tests.EditMode.Fakes;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class NetSwingPredictionTests
    {
        private GameObject _object = null!;
        private IAbilityControllerBase _controller = null!;
        private ClientTagState _clientTags = null!;
        private readonly HashSet<string> _serverTags = new();
        private AbilityDefinition _swing = null!;
        private GameplayTag _carryingTag = null!;
        private GameplayTag _swingingTag = null!;
        private FakeTimeService _time = null!;
        private AbilitySystemUseCase _abilities = null!;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("NetPredictionTest");
            var mediatorType = Type.GetType("TinCan.Network.Infrastructure.Abilities.AbilityNetworkMediator, Assembly-CSharp", true)!;
            _controller = (IAbilityControllerBase)_object.AddComponent(mediatorType);
            // Isolate an owning client's tag storage without starting a transport. Any attempted RPC is an error.
            typeof(NetworkBehaviour).GetProperty("IsOwner")!.SetValue(_controller, true);
            _clientTags = (ClientTagState)mediatorType.GetField("_clientTags", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_controller)!;
            _serverTags.Clear();
            _swing = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Abilities/AbilityDefinitions/GA_SwingNet.asset"));
            _swing.TriggerInput = Object.Instantiate(_swing.TriggerInput);
            _swing.TriggerInput.BitIndex = 0;
            _carryingTag = _swing.ActivationRequiredTagsOnActor[0];
            _swingingTag = _swing.ActiveEffect.GrantedTags[0];
            _time = new FakeTimeService();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), new FakeActorRegistry(), _time, new FakeEventPublisher());
            _abilities.GrantAbility(_controller, _swing);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_swing.TriggerInput);
            Object.DestroyImmediate(_swing);
        }

        // Stands in for the server's replicated tag set arriving on this client.
        private void ServerSets(string tag, bool present)
        {
            if (present) _serverTags.Add(tag); else _serverTags.Remove(tag);
            _clientTags.SetReplicated(_serverTags);
        }

        [Test]
        public void Input_WithSynchronizedCarryTag_PredictsAndExpiresSwingLocally()
        {
            ServerSets(_carryingTag.name, true);
            Assert.That(_controller.ActiveTags.HasTag(_carryingTag), Is.False, "The client's raw domain container is not its synchronized tag store.");

            _abilities.ProcessAbilitySimulation(_controller, new HumanoidInputState { ActiveInputMask = 1 }, 0, _time.DeltaTime);

            Assert.That(_controller.HasTag(_swingingTag), Is.True, "The owner must see the swing before receiving a server confirmation.");
            _time.Time = _swing.ActiveEffect.DurationSeconds + 0.01f;
            _abilities.ProcessAbilitySimulation(_controller, default, 1, _time.DeltaTime);
            Assert.That(_controller.HasTag(_swingingTag), Is.False);
        }

        [Test]
        public void Input_WithoutCarryTag_DoesNotPredictSwing()
        {
            _abilities.ProcessAbilitySimulation(_controller, new HumanoidInputState { ActiveInputMask = 1 }, 0, _time.DeltaTime);

            Assert.That(_controller.HasTag(_swingingTag), Is.False);
        }

        [Test]
        public void Input_WithSynchronizedBlockedTag_DoesNotPredictSwing()
        {
            _swing.ActivationBlockedTagsOnActor.Add(_carryingTag);
            ServerSets(_carryingTag.name, true);

            _abilities.ProcessAbilitySimulation(_controller, new HumanoidInputState { ActiveInputMask = 1 }, 0, _time.DeltaTime);

            Assert.That(_controller.HasTag(_swingingTag), Is.False);
        }

        [Test]
        public void ExpiringPrediction_DoesNotRemoveServerConfirmation()
        {
            _controller.AddEffectTag(_swingingTag);
            ServerSets(_swingingTag.name, true);

            _controller.RemoveEffectTag(_swingingTag);

            Assert.That(_controller.HasTag(_swingingTag), Is.True);
            ServerSets(_swingingTag.name, false);
            Assert.That(_controller.HasTag(_swingingTag), Is.False);
        }
    }
}
