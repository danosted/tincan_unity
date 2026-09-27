#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Targeting;

namespace TinCan.Features.Targeting
{
    /// <summary>
    /// Every live <see cref="ITargetable"/>, filled by ActorOrchestrator. A targetable can sit under more than one
    /// registered hierarchy (a ship part under the ship and under its own socket), so registration is idempotent.
    /// </summary>
    public sealed class TargetableRegistry : ITargetableRegistry
    {
        private readonly HashSet<ITargetable> _targetables = new();

        public IReadOnlyCollection<ITargetable> All => _targetables;

        public void Register(ITargetable targetable) => _targetables.Add(targetable);

        public void Unregister(ITargetable targetable) => _targetables.Remove(targetable);
    }
}
