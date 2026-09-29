#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;
using TinCan.Core.Domain.Features;
using TinCan.Core.Gas.Cues;
using TinCan.Core.Gas.Cues.Actions;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>The use case turns cue tags into Active/Removed on every peer and plays bursts; despawn is silent.</summary>
    public class GameplayCueUseCaseTests
    {
        private sealed class CueInstaller : FeatureInstaller, FeatureInstaller.IExtension<GameplayCueNotify>
        {
            public readonly List<GameplayCueNotify> Notifies = new();
            public override void Install(IContainerBuilder builder) { }
            IEnumerable<GameplayCueNotify> FeatureInstaller.IExtension<GameplayCueNotify>.Contributions => Notifies;
        }

        private readonly List<Object> _assets = new();
        private readonly List<(GameplayCueEventKind Kind, GameplayCueEvent Event)> _handled = new();
        private GameplayTag _broken = null!;
        private GameplayTag _unlisted = null!;
        private FakeAbilityRegistry _abilities = null!;
        private FakeActorRegistry _actors = null!;
        private FakeSessionNetworkService _network = null!;
        private GameplayCueUseCase _cues = null!;
        private RecordingCuePresenter _presenter = null!;

        [SetUp]
        public void SetUp()
        {
            _broken = Create<GameplayTag>();
            _unlisted = Create<GameplayTag>();
            var installer = Create<CueInstaller>();
            var notify = Notify(_broken);
            typeof(GameplayCueNotify).GetField("_onActive", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(notify, new List<GameplayCueAction?> { new HudToastCueAction { Text = "Broken", Seconds = 1f } });
            installer.Notifies.Add(notify);
            _presenter = new RecordingCuePresenter();

            _abilities = new FakeAbilityRegistry();
            _actors = new FakeActorRegistry();
            _network = new FakeSessionNetworkService { IsServer = true, IsClient = true };
            var catalog = new GameplayCueCatalog(new FeatureInstallerCatalog(new FeatureInstaller[] { installer }));
            _cues = new GameplayCueUseCase(_abilities, _actors, _network, catalog, new GameplayCueStateTracker(), _presenter);
            _cues.Initialize();
            _cues.CueHandled += (kind, cueEvent) => _handled.Add((kind, cueEvent));
        }

        [TearDown]
        public void TearDown()
        {
            _cues.Dispose();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
            _handled.Clear();
        }

        [Test]
        public void CueTag_Appearing_IsActiveOnce_Leaving_IsRemovedOnce()
        {
            var part = Register(new FakeAbilityController());

            _cues.Tick();
            Assert.That(_handled, Is.Empty);

            part.AddTag(_broken);
            _cues.Tick();
            _cues.Tick();
            Assert.That(_handled, Has.Count.EqualTo(1));
            Assert.That(_handled[0].Kind, Is.EqualTo(GameplayCueEventKind.Active));
            Assert.That(_handled[0].Event.Controller, Is.SameAs(part));
            Assert.That(_handled[0].Event.Cue, Is.SameAs(_broken));

            part.RemoveTag(_broken);
            _cues.Tick();
            Assert.That(_handled, Has.Count.EqualTo(2));
            Assert.That(_handled[1].Kind, Is.EqualTo(GameplayCueEventKind.Removed));
        }

        [Test]
        public void CatalogNotifies_RunWithTheEdge()
        {
            var part = Register(new FakeAbilityController());
            part.AddTag(_broken);

            _cues.Tick();

            Assert.That(_presenter.Calls, Is.EqualTo(new[] { "hud Broken 1" }));
        }

        [Test]
        public void ActorJoiningWithTheCue_GetsActiveOnItsFirstFrame()
        {
            var part = new FakeAbilityController();
            part.AddTag(_broken);
            Register(part);

            _cues.Tick();

            Assert.That(_handled, Has.Count.EqualTo(1));
            Assert.That(_handled[0].Kind, Is.EqualTo(GameplayCueEventKind.Active));
        }

        [Test]
        public void Despawn_ForgetsTheCue_WithoutRemoved()
        {
            var part = Register(new FakeAbilityController());
            part.AddTag(_broken);
            _cues.Tick();

            _abilities.Unregister(part);
            _actors.Unregister(part);
            _cues.Tick();

            Assert.That(_handled, Has.Count.EqualTo(1), "only the Active edge; teardown is not a removal");
            Assert.That(_presenter.Calls, Does.Contain("release actor"), "held presentation goes back to the pool");
        }

        [Test]
        public void TagsNoHandlerPresents_AreNotWatched()
        {
            var part = Register(new FakeAbilityController());
            part.AddTag(_unlisted);

            _cues.Tick();

            Assert.That(_handled, Is.Empty);
        }

        [Test]
        public void Play_IsAnExecute_WithThePeersRole()
        {
            var owned = new FakeRelayController { IsOwnedLocally = true };
            _network.IsServer = false;

            _cues.Play(_broken, owned);
            _cues.Play(_broken, new FakeRelayController());

            Assert.That(_handled[0].Kind, Is.EqualTo(GameplayCueEventKind.Execute));
            Assert.That(_handled[0].Event.Role, Is.EqualTo(GameplayCuePeerRole.Owner));
            Assert.That(_handled[1].Event.Role, Is.EqualTo(GameplayCuePeerRole.Proxy));
        }

        [Test]
        public void ServerRole_OnTheServer()
        {
            _cues.Play(_broken, new FakeRelayController { IsOwnedLocally = true });

            Assert.That(_handled[0].Event.Role, Is.EqualTo(GameplayCuePeerRole.Server));
        }

        private FakeAbilityController Register(FakeAbilityController controller)
        {
            _abilities.Register(controller);
            _actors.Register(controller);
            return controller;
        }

        private GameplayCueNotify Notify(GameplayTag cue)
        {
            var notify = Create<GameplayCueNotify>();
            typeof(GameplayCueNotify).GetField("_cue", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(notify, cue);
            return notify;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
