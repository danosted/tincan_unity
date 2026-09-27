#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities.Cues;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>The dispatcher follows the processor's decision: play here, relay to clients, leave the owner out.</summary>
    public class GameplayCueDispatcherTests
    {
        private GameplayTag _cue = null!;
        private FakeSessionNetworkService _network = null!;
        private RecordingCuePlayer _player = null!;
        private GameplayCueDispatcher _dispatcher = null!;
        private FakeRelayController _target = null!;

        [SetUp]
        public void SetUp()
        {
            _cue = ScriptableObject.CreateInstance<GameplayTag>();
            _network = new FakeSessionNetworkService();
            _player = new RecordingCuePlayer();
            _dispatcher = new GameplayCueDispatcher(_network, new GameplayCueDispatchProcessor(), _player);
            _target = new FakeRelayController();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cue);

        [Test]
        public void Host_PlaysAndRelaysToAll()
        {
            _network.IsServer = _network.IsClient = true;

            _dispatcher.Execute(_cue, _target, GameplayEffectContext.Default);

            Assert.That(_player.Played, Has.Count.EqualTo(1));
            Assert.That(_target.Relayed, Has.Count.EqualTo(1));
            Assert.That(_target.Relayed[0].ExcludeOwner, Is.False);
        }

        [Test]
        public void Host_Predicted_RelaysWithoutTheOwner()
        {
            _network.IsServer = _network.IsClient = true;

            _dispatcher.Execute(_cue, _target, GameplayEffectContext.Predicted);

            Assert.That(_target.Relayed[0].ExcludeOwner, Is.True);
        }

        [Test]
        public void OwningClient_Predicted_PlaysItself_RelaysNothing()
        {
            _network.IsClient = true;
            _target.IsOwnedLocally = true;

            _dispatcher.Execute(_cue, _target, GameplayEffectContext.Predicted);

            Assert.That(_player.Played, Has.Count.EqualTo(1));
            Assert.That(_target.Relayed, Is.Empty);
        }

        [Test]
        public void Client_NotPredicted_WaitsForTheServer()
        {
            _network.IsClient = true;
            _target.IsOwnedLocally = true;

            _dispatcher.Execute(_cue, _target, GameplayEffectContext.Default);

            Assert.That(_player.Played, Is.Empty);
            Assert.That(_target.Relayed, Is.Empty);
        }

        [Test]
        public void Offline_PlaysLocally()
        {
            _network.IsActive = false;

            _dispatcher.Execute(_cue, new FakeAbilityController(), GameplayEffectContext.Default);

            Assert.That(_player.Played, Has.Count.EqualTo(1));
        }

        [Test]
        public void Server_TargetWithoutRelay_StillPlaysOnTheHost()
        {
            _network.IsServer = _network.IsClient = true;

            Assert.DoesNotThrow(() => _dispatcher.Execute(_cue, new FakeAbilityController(), GameplayEffectContext.Default));
            Assert.That(_player.Played, Has.Count.EqualTo(1));
        }
    }
}
