#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace TinCan.Tests.EditMode
{
    public class GameplayTagContainerTests
    {
        private readonly List<Object> _created = new();
        private GameplayTag _state = null!;
        private GameplayTag _stunned = null!;
        private GameplayTag _carrying = null!;
        private GameplayTag _other = null!;

        [SetUp]
        public void SetUp()
        {
            _state = Tag("State");
            _stunned = Tag("State.Stunned", _state);
            _carrying = Tag("State.Carrying", _state);
            _other = Tag("Ability.Sprint");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created) Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void HasTag_MatchesTheTagAndItsParents()
        {
            var container = new GameplayTagContainer(new[] { _stunned });

            Assert.That(container.HasTag(_stunned), Is.True);
            Assert.That(container.HasTag(_state), Is.True, "a child tag counts as its parent");
            Assert.That(container.HasTag(_carrying), Is.False, "a sibling does not");
            Assert.That(container.HasTag(_other), Is.False);
        }

        [Test]
        public void HasTag_NullOrEmpty_IsFalse()
        {
            Assert.That(new GameplayTagContainer(new[] { _stunned }).HasTag(null!), Is.False);
            Assert.That(new GameplayTagContainer(null).HasTag(_state), Is.False);
            Assert.That(default(GameplayTagContainer).HasTag(_state), Is.False);
        }

        [Test]
        public void HasAny_TrueWhenOneQueryTagMatches()
        {
            var container = new GameplayTagContainer(new[] { _stunned });

            Assert.That(container.HasAny(new GameplayTagContainer(new[] { _other, _state })), Is.True);
            Assert.That(container.HasAny(new GameplayTagContainer(new[] { _other, _carrying })), Is.False);
            Assert.That(container.HasAny(default), Is.False);
        }

        [Test]
        public void HasAll_TrueOnlyWhenEveryQueryTagMatches()
        {
            var container = new GameplayTagContainer(new[] { _stunned, _other });

            Assert.That(container.HasAll(new GameplayTagContainer(new[] { _state, _other })), Is.True);
            Assert.That(container.HasAll(new GameplayTagContainer(new[] { _state, _carrying })), Is.False);
            Assert.That(container.HasAll(new GameplayTagContainer(new GameplayTag[0])), Is.True, "nothing to require");
        }

        [Test]
        public void Queries_DoNotAllocate()
        {
            var container = new GameplayTagContainer(new[] { _stunned, _other });
            var query = new GameplayTagContainer(new[] { _state, _other });
            container.HasTag(_state);
            container.HasAny(query);
            container.HasAll(query);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    container.HasTag(_state);
                    container.HasAny(query);
                    container.HasAll(query);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        private GameplayTag Tag(string name, GameplayTag? parent = null)
        {
            var tag = ScriptableObject.CreateInstance<GameplayTag>();
            tag.name = name;
            _created.Add(tag);
            if (parent == null) return tag;

            var serialized = new SerializedObject(tag);
            serialized.FindProperty("_parent").objectReferenceValue = parent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tag;
        }
    }
}
