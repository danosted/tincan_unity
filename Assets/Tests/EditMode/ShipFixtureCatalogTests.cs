#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Features.Airship.Fixtures;
using UnityEngine;
using VContainer;

namespace TinCan.Tests.EditMode
{
    /// <summary>Covers ShipFixtureCatalog's discovery of installers that opt into FeatureInstaller.IExtension&lt;ShipFixtureDefinition&gt;.</summary>
    public class ShipFixtureCatalogTests
    {
        private sealed class PlainInstaller : FeatureInstaller
        {
            public override void Install(IContainerBuilder builder) { }
        }

        private sealed class FixtureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFixtureDefinition>
        {
            public ShipFixtureDefinition? Fixture;
            public override void Install(IContainerBuilder builder) { }

            IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
            {
                get { if (Fixture != null) yield return Fixture; }
            }
        }

        private readonly List<Object> _assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Fixtures_OnlyIncludesInstallersThatOptIntoTheExtension()
        {
            var fixture = Create<ShipFixtureDefinition>();
            var contributor = Create<FixtureInstaller>();
            contributor.Fixture = fixture;
            var plain = Create<PlainInstaller>();

            var installers = new FeatureInstallerCatalog(new FeatureInstaller[] { contributor, plain });
            var catalog = new ShipFixtureCatalog(installers);

            Assert.That(catalog.Fixtures, Is.EqualTo(new[] { fixture }));
        }

        [Test]
        public void Fixtures_EmptyWhenNoInstallerContributes()
        {
            var installers = new FeatureInstallerCatalog(new FeatureInstaller[] { Create<PlainInstaller>() });
            var catalog = new ShipFixtureCatalog(installers);

            Assert.That(catalog.Fixtures, Is.Empty);
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }
    }
}
