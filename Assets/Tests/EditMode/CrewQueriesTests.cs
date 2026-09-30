#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="CrewQueries"/>. Plan: <c>crew-gate-and-boarding.md</c>.</summary>
    public class CrewQueriesTests
    {
        [Test]
        public void CrewCount_CountsOnlyPlayerCharacters()
        {
            var actors = new FakeActorRegistry();
            actors.Register(new FakeCrewMember());
            actors.Register(new FakeCrewMember());
            actors.Register(new FakeCrewMember(isPlayerCharacter: false));

            Assert.That(actors.CrewCount(), Is.EqualTo(2));
        }

        [Test]
        public void CrewCount_IsZero_WithNobodyAboard()
        {
            Assert.That(new FakeActorRegistry().CrewCount(), Is.Zero);
        }
    }
}
