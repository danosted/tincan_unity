#nullable enable
using System;
using TinCan.Core.Domain;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A body in the world for crew counts (<see cref="CrewQueries"/>): a player's, or with false, nobody's.</summary>
    public class FakeCrewMember : IHumanoidActor
    {
        public FakeCrewMember(bool isPlayerCharacter = true)
        {
            IsPlayerCharacter = isPlayerCharacter;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public bool IsPlayerCharacter { get; }
    }
}
