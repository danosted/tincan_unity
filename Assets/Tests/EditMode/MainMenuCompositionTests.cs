#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Features;
using TinCan.Core.UI;
using UnityEngine;
using VContainer;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="MainMenuComposition"/>: loaded features add main-menu rows before Quit.</summary>
    public class MainMenuCompositionTests
    {
        private sealed class RowsInstaller : FeatureInstaller, IMainMenuRows
        {
            public override void Install(IContainerBuilder builder) { }

            public IEnumerable<MenuItemDefinition> MainMenuRows => new[]
            {
                new MenuItemDefinition { ItemId = "shipyard", Label = "Shipyard", Kind = MenuItemKind.Command, CommandId = "OpenShipyard" },
            };
        }

        private sealed class PlainInstaller : FeatureInstaller
        {
            public override void Install(IContainerBuilder builder) { }
        }

        private MenuDefinition _main = null!;
        private readonly List<Object> _objects = new();

        [SetUp]
        public void SetUp()
        {
            _main = MenuDefinition.Create("main", "TinCan",
                new MenuItemDefinition { ItemId = "host", Kind = MenuItemKind.Command, CommandId = "StartHost" },
                new MenuItemDefinition { ItemId = "quit", Kind = MenuItemKind.Command, CommandId = "Quit" });
            _objects.Add(_main);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) Object.DestroyImmediate(o);
            _objects.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _objects.Add(made);
            return made;
        }

        [Test]
        public void WithoutContributions_TheAuthoredMenuIsUsed()
        {
            Assert.That(MainMenuComposition.Compose(_main, new FeatureInstaller[] { Make<PlainInstaller>() }), Is.SameAs(_main));
            Assert.That(MainMenuComposition.Compose(_main, null), Is.SameAs(_main));
        }

        [Test]
        public void ContributedRows_GoBeforeQuit_AndTheAssetIsUntouched()
        {
            var composed = MainMenuComposition.Compose(_main, new FeatureInstaller[] { Make<RowsInstaller>() });
            _objects.Add(composed);

            Assert.That(composed.Items.Select(i => i.ItemId), Is.EqualTo(new[] { "host", "shipyard", "quit" }));
            Assert.That((composed.MenuId, composed.Title), Is.EqualTo(("main", "TinCan")));
            Assert.That(_main.Items.Count, Is.EqualTo(2));
        }
    }
}
