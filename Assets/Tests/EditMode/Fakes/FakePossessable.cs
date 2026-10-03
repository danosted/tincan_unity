#nullable enable
using System;
using TinCan.Core.Domain;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Something other than a body that a player can possess (the free camera, in the game).</summary>
    public sealed class FakePossessable : IPossessable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public ulong? PossessorId { get; private set; }

        public void AuthoritativeSetPossessor(ulong? playerId) => PossessorId = playerId;
        public bool CanPossess(ulong playerId) => true;
    }
}
