#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using TinCan.Features.DesignedEvents;

namespace TinCan.Tests.EditMode
{
    public class EventDefinitionBuilderTests
    {
        private sealed class Never : IEventCondition
        {
        }

        [Test]
        public void Build_KeepsPhasesAndOutcomeActionsInOrder()
        {
            var definition = new EventDefinition.Builder(7, "Test")
                .Phase("A", p => p.OnEnter(new Announce("a")).Duration(1f))
                .Phase("B", p => p.Until(new Never()).Timeout(5f))
                .OnSuccess(new Announce("won"))
                .OnFailure(new Announce("lost"))
                .Build();

            Assert.That(definition.Id, Is.EqualTo(7));
            Assert.That(definition.Phases.Select(phase => phase.Name), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(definition.Phases[1].EndCondition, Is.TypeOf<Never>());
            Assert.That(definition.Phases[1].Seconds, Is.EqualTo(5f));
            Assert.That(((Announce)definition.SuccessActions.Single()).Text, Is.EqualTo("won"));
            Assert.That(((Announce)definition.FailureActions.Single()).Text, Is.EqualTo("lost"));
            Assert.That(definition.Steps.Count(), Is.EqualTo(4), "a, Never, won, lost");
        }

        [Test]
        public void Build_Rejects_NonPositiveId() =>
            AssertInvalid(new EventDefinition.Builder(0, "Test").Phase("A", p => p.Duration(1f)), "id must be positive");

        [Test]
        public void Build_Rejects_NoPhases() =>
            AssertInvalid(new EventDefinition.Builder(1, "Test"), "at least one phase");

        [Test]
        public void Build_Rejects_DuplicatePhaseNames() =>
            AssertInvalid(new EventDefinition.Builder(1, "Test")
                .Phase("A", p => p.Duration(1f))
                .Phase("A", p => p.Duration(1f)), "'A' is used twice");

        [Test]
        public void Build_Rejects_TimedPhaseWithoutDuration() =>
            AssertInvalid(new EventDefinition.Builder(1, "Test").Phase("A", p => p), "no Duration");

        [Test]
        public void Build_Rejects_ConditionPhaseWithoutTimeout() =>
            AssertInvalid(new EventDefinition.Builder(1, "Test").Phase("A", p => p.Until(new Never())), "no Timeout");

        [Test]
        public void Builder_ChangesAfterBuild_DoNotReachTheDefinition()
        {
            var builder = new EventDefinition.Builder(1, "Test").Phase("A", p => p.Duration(1f));
            var definition = builder.Build();

            builder.Phase("B", p => p.Duration(1f)).OnSuccess(new Announce("late"));

            Assert.That(definition.Phases.Count, Is.EqualTo(1));
            Assert.That(definition.SuccessActions, Is.Empty);
        }

        private static void AssertInvalid(EventDefinition.Builder builder, string message)
        {
            var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
            Assert.That(error!.Message, Does.Contain(message));
        }
    }
}
