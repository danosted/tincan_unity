#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class GameplayTagRegistryTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        private GameplayTag Tag(string name)
        {
            var tag = ScriptableObject.CreateInstance<GameplayTag>();
            tag.name = name;
            _created.Add(tag);
            return tag;
        }

        [Test]
        public void TryGet_FindsTagsByExactName()
        {
            var building = Tag("State.Building");
            var registry = new GameplayTagRegistry(new[] { building, Tag("State.Sprinting") });

            Assert.That(registry.TryGet("State.Building", out var found), Is.True);
            Assert.That(found, Is.SameAs(building));
            Assert.That(registry.TryGet("state.building", out _), Is.False);
            Assert.That(registry.All.Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateNames_FirstWinsAndIsReported()
        {
            var first = Tag("State.Damaged");
            var registry = new GameplayTagRegistry(new[] { first, Tag("State.Damaged"), null });

            Assert.That(registry.TryGet("State.Damaged", out var found), Is.True);
            Assert.That(found, Is.SameAs(first));
            Assert.That(registry.Duplicates, Is.EqualTo(new[] { "State.Damaged" }));
        }

        [Test]
        public void UnknownName_IsNotFound()
        {
            var registry = new GameplayTagRegistry(new GameplayTag?[0]);

            Assert.That(registry.TryGet("Nope", out _), Is.False);
        }
    }
}
