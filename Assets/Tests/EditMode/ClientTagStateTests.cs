#nullable enable
using NUnit.Framework;
using TinCan.Features.Abilities;

namespace TinCan.Tests.EditMode
{
    public class ClientTagStateTests
    {
        [Test]
        public void ReplicatedSet_IsVisible_IncludingToALateJoiner()
        {
            var state = new ClientTagState();

            state.SetReplicated(new[] { "State.Damaged", "State.Ship.Damaged" });

            Assert.That(state.Has("State.Damaged"), Is.True);
            Assert.That(state.Has("State.Ship.Damaged"), Is.True);
            Assert.That(state.Has("State.Repairing"), Is.False);
        }

        [Test]
        public void ReplicatedRemoval_HidesTheTag()
        {
            var state = new ClientTagState();
            state.SetReplicated(new[] { "State.Damaged" });

            state.SetReplicated(new string[0]);

            Assert.That(state.Has("State.Damaged"), Is.False);
        }

        [Test]
        public void OptimisticAdd_ShowsAtOnce_AndTheServerAnswerTakesOver()
        {
            var state = new ClientTagState();

            state.AddOptimistic("State.Building");
            Assert.That(state.Has("State.Building"), Is.True, "owner sees its request at once");

            state.SetReplicated(new[] { "State.Building" });
            state.SetReplicated(new string[0]);
            Assert.That(state.Has("State.Building"), Is.False, "the server's later removal wins");
        }

        [Test]
        public void OptimisticRemove_HidesAReplicatedTag_UntilTheServerAnswers()
        {
            var state = new ClientTagState();
            state.SetReplicated(new[] { "State.Building" });

            state.RemoveOptimistic("State.Building");
            Assert.That(state.Has("State.Building"), Is.False);

            state.SetReplicated(new string[0]);
            state.SetReplicated(new[] { "State.Building" });
            Assert.That(state.Has("State.Building"), Is.True, "a later server add is not masked by the old request");
        }

        [Test]
        public void UnrelatedReplicatedChange_KeepsPendingOptimisticEntries()
        {
            var state = new ClientTagState();
            state.AddOptimistic("State.Building");

            state.SetReplicated(new[] { "State.Damaged" });

            Assert.That(state.Has("State.Building"), Is.True);
        }

        [Test]
        public void PredictedTags_AreIndependentOfReplication()
        {
            var state = new ClientTagState();

            state.AddPredicted("State.Repairing");
            state.SetReplicated(new string[0]);
            Assert.That(state.Has("State.Repairing"), Is.True);

            state.RemovePredicted("State.Repairing");
            Assert.That(state.Has("State.Repairing"), Is.False);
        }

        [Test]
        public void PredictedRemoval_DoesNotHideTheServersTag()
        {
            var state = new ClientTagState();
            state.SetReplicated(new[] { "State.Repairing" });
            state.AddPredicted("State.Repairing");

            state.RemovePredicted("State.Repairing");

            Assert.That(state.Has("State.Repairing"), Is.True, "stays until the server removes it");
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var state = new ClientTagState();
            state.SetReplicated(new[] { "A" });
            state.AddOptimistic("B");
            state.AddPredicted("C");

            state.Clear();

            Assert.That(state.Has("A") || state.Has("B") || state.Has("C"), Is.False);
        }
    }
}
