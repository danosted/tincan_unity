#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using VContainer;

namespace TinCan.Tests.EditMode
{
    /// <summary>Covers ActorAbilityGrantUseCase: features grant abilities to each humanoid or airship as it registers.</summary>
    public class ActorAbilityGrantUseCaseTests
    {
        private sealed class PlainInstaller : FeatureInstaller
        {
            public override void Install(IContainerBuilder builder) { }
        }

        private sealed class GrantingInstaller : FeatureInstaller, FeatureInstaller.IExtension<ActorAbilityGrant>
        {
            public readonly List<ActorAbilityGrant?> Grants = new();
            public override void Install(IContainerBuilder builder) { }
            IEnumerable<ActorAbilityGrant> FeatureInstaller.IExtension<ActorAbilityGrant>.Contributions => Grants!;
        }

        private readonly List<Object> _assets = new();
        private FakeActorRegistry _actors = null!;
        private FakeHumanoidMovementView _movement = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _movement = new FakeHumanoidMovementView("Player");
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void RegisteredHumanoid_GetsTheHumanoidGrants_Only()
        {
            var repair = Create<AbilityDefinition>();
            var ram = Create<AbilityDefinition>();
            var feature = Create<GrantingInstaller>();
            feature.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Humanoid, Ability = repair });
            feature.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Airship, Ability = ram });
            var useCase = Start(feature, Create<PlainInstaller>());

            var player = new FakeHumanoidCharacterView(_movement);
            _actors.Register(player);

            Assert.That(player.GrantedAbilities, Is.EqualTo(new[] { repair }));
            Assert.That(useCase.For(ActorKind.Airship), Is.EqualTo(new[] { ram }));
        }

        [Test]
        public void ActorsRegisteredBeforeStart_AreGrantedToo()
        {
            var repair = Create<AbilityDefinition>();
            var feature = Create<GrantingInstaller>();
            feature.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Humanoid, Ability = repair });
            var player = new FakeHumanoidCharacterView(_movement);
            _actors.Register(player);

            Start(feature);

            Assert.That(player.GrantedAbilities, Is.EqualTo(new[] { repair }));
        }

        [Test]
        public void EmptyEntries_AreSkipped_AndASharedAbilityIsGrantedOnce()
        {
            var shared = Create<AbilityDefinition>();
            var a = Create<GrantingInstaller>();
            a.Grants.Add(null);
            a.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Humanoid, Ability = null });
            a.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Humanoid, Ability = shared });
            var b = Create<GrantingInstaller>();
            b.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Humanoid, Ability = shared });
            Start(a, b);

            var player = new FakeHumanoidCharacterView(_movement);
            _actors.Register(player);

            Assert.That(player.GrantedAbilities, Is.EqualTo(new[] { shared }));
        }

        [Test]
        public void Dispose_StopsGranting()
        {
            var feature = Create<GrantingInstaller>();
            feature.Grants.Add(new ActorAbilityGrant { Actor = ActorKind.Humanoid, Ability = Create<AbilityDefinition>() });
            Start(feature).Dispose();

            var player = new FakeHumanoidCharacterView(_movement);
            _actors.Register(player);

            Assert.That(player.GrantedAbilities, Is.Empty);
        }

        private ActorAbilityGrantUseCase Start(params FeatureInstaller[] installers)
        {
            var useCase = new ActorAbilityGrantUseCase(_actors, new FeatureInstallerCatalog(installers));
            useCase.Initialize();
            return useCase;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
