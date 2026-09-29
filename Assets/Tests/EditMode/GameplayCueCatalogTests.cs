#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Core.Gas.Cues;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>Cue notifies come only from installers in the profile that contribute them.</summary>
    public class GameplayCueCatalogTests
    {
        private sealed class PlainInstaller : FeatureInstaller
        {
            public override void Install(IContainerBuilder builder) { }
        }

        private sealed class CueInstaller : FeatureInstaller, FeatureInstaller.IExtension<GameplayCueNotify>
        {
            public readonly List<GameplayCueNotify?> Notifies = new();
            public override void Install(IContainerBuilder builder) { }

            IEnumerable<GameplayCueNotify> FeatureInstaller.IExtension<GameplayCueNotify>.Contributions => Notifies!;
        }

        private readonly List<Object> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void For_ReturnsEveryContributedNotifyOfThatCue()
        {
            var broken = Create<GameplayTag>();
            var leak = Create<GameplayTag>();
            var sparks = Notify(broken);
            var ding = Notify(broken);
            var hiss = Notify(leak);
            var damage = Create<CueInstaller>();
            damage.Notifies.AddRange(new[] { sparks, hiss });
            var other = Create<CueInstaller>();
            other.Notifies.Add(ding);

            var catalog = new GameplayCueCatalog(new FeatureInstallerCatalog(new FeatureInstaller[] { damage, other, Create<PlainInstaller>() }));

            Assert.That(catalog.For(broken), Is.EquivalentTo(new[] { sparks, ding }));
            Assert.That(catalog.For(leak), Is.EquivalentTo(new[] { hiss }));
            Assert.That(catalog.Cues, Is.EquivalentTo(new[] { broken, leak }));
        }

        [Test]
        public void AbsentInstaller_ContributesNothing()
        {
            var broken = Create<GameplayTag>();
            var damage = Create<CueInstaller>();
            damage.Notifies.Add(Notify(broken));

            var catalog = new GameplayCueCatalog(new FeatureInstallerCatalog(new FeatureInstaller[] { Create<PlainInstaller>() }));

            Assert.That(catalog.For(broken), Is.Empty);
            Assert.That(catalog.Cues, Is.Empty);
        }

        [Test]
        public void NotifyWithoutCue_IsIgnoredAndReported_NullsAndDuplicatesAreSkipped()
        {
            var broken = Create<GameplayTag>();
            var sparks = Notify(broken);
            var untagged = Notify(null);
            var damage = Create<CueInstaller>();
            damage.Notifies.AddRange(new[] { sparks, sparks, null, untagged });

            var catalog = new GameplayCueCatalog(new FeatureInstallerCatalog(new FeatureInstaller[] { damage }));

            Assert.That(catalog.For(broken), Is.EquivalentTo(new[] { sparks }));
            Assert.That(catalog.Problems, Has.Count.EqualTo(1));
        }

        private GameplayCueNotify Notify(GameplayTag? cue)
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
