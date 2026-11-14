#nullable enable
using NUnit.Framework;
using TinCan.Core.Gas;

namespace TinCan.Core.Gas.Tests
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
        public void AShortPress_IsNotTurnedBackOnByTheServersLateCopy()
        {
            var state = new ClientTagState();
            state.AddPredicted("State.Repairing");
            state.RemovePredicted("State.Repairing"); // let go before the server showed it

            state.SetReplicated(new[] { "State.Repairing" }); // the server's copy of that press, late

            Assert.That(state.Has("State.Repairing"), Is.False);

            state.SetReplicated(new string[0]); // the server lets go too
            state.SetReplicated(new[] { "State.Repairing" }); // a later server-side tag is real again

            Assert.That(state.Has("State.Repairing"), Is.True);
        }

        [Test]
        public void ANewPress_TakesTheServersCopyAgain()
        {
            var state = new ClientTagState();
            state.AddPredicted("State.Repairing");
            state.RemovePredicted("State.Repairing");
            state.SetReplicated(new[] { "State.Repairing" });

            state.AddPredicted("State.Repairing");
            state.RemovePredicted("State.Repairing"); // the server already shows it: it stays until the server ends it

            Assert.That(state.Has("State.Repairing"), Is.True);
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var state = new ClientTagState();
            state.SetReplicated(new[] { "A" });
            state.AddPredicted("C");

            state.Clear();

            Assert.That(state.Has("A") || state.Has("C"), Is.False);
        }
    }
}
