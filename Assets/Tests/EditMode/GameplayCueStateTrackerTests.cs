#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities.Cues;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class GameplayCueStateTrackerTests
    {
        private GameplayTag _broken = null!;
        private GameplayTag _leak = null!;
        private readonly Guid _part = Guid.NewGuid();
        private readonly Guid _ship = Guid.NewGuid();

        [SetUp]
        public void SetUp()
        {
            _broken = ScriptableObject.CreateInstance<GameplayTag>();
            _leak = ScriptableObject.CreateInstance<GameplayTag>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_broken);
            Object.DestroyImmediate(_leak);
        }

        [Test]
        public void Appearing_IsActive_ThenUnchanged_IsNone()
        {
            var tracker = new GameplayCueStateTracker();

            Assert.That(tracker.Observe(_part, _broken, present: true), Is.EqualTo(GameplayCueStateEdge.Active));
            Assert.That(tracker.Observe(_part, _broken, present: true), Is.EqualTo(GameplayCueStateEdge.None));
            Assert.That(tracker.IsActive(_part, _broken), Is.True);
        }

        [Test]
        public void Leaving_IsRemoved_ThenUnchanged_IsNone()
        {
            var tracker = new GameplayCueStateTracker();
            tracker.Observe(_part, _broken, present: true);

            Assert.That(tracker.Observe(_part, _broken, present: false), Is.EqualTo(GameplayCueStateEdge.Removed));
            Assert.That(tracker.Observe(_part, _broken, present: false), Is.EqualTo(GameplayCueStateEdge.None));
            Assert.That(tracker.IsActive(_part, _broken), Is.False);
        }

        [Test]
        public void NeverPresent_IsNone()
        {
            var tracker = new GameplayCueStateTracker();

            Assert.That(tracker.Observe(_part, _broken, present: false), Is.EqualTo(GameplayCueStateEdge.None));
        }

        [Test]
        public void ActorsAndCues_AreTrackedIndependently()
        {
            var tracker = new GameplayCueStateTracker();
            tracker.Observe(_part, _broken, present: true);

            Assert.That(tracker.Observe(_ship, _broken, present: true), Is.EqualTo(GameplayCueStateEdge.Active), "same cue, other actor");
            Assert.That(tracker.Observe(_part, _leak, present: true), Is.EqualTo(GameplayCueStateEdge.Active), "same actor, other cue");
            Assert.That(tracker.Observe(_part, _leak, present: false), Is.EqualTo(GameplayCueStateEdge.Removed));
            Assert.That(tracker.IsActive(_part, _broken), Is.True, "removing one cue keeps the actor's others");
        }

        [Test]
        public void Forget_DropsTheActorWithoutARemovedEdge()
        {
            var tracker = new GameplayCueStateTracker();
            tracker.Observe(_part, _broken, present: true);

            tracker.Forget(_part);

            Assert.That(tracker.IsActive(_part, _broken), Is.False);
            Assert.That(tracker.Observe(_part, _broken, present: false), Is.EqualTo(GameplayCueStateEdge.None), "teardown is not a removal");
        }
    }
}
