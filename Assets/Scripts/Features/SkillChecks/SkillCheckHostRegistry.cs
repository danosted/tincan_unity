#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Every peer: the spawned skill check hosts. A host adds itself when it spawns and leaves when it despawns, so a
    /// host that is not its entity's actor is found too (a broken part's repair check sits beside the part's own GAS
    /// actor, and the actor registry holds one actor per entity).
    /// </summary>
    public sealed class SkillCheckHostRegistry
    {
        private readonly List<ISkillCheckHost> _hosts = new();

        public IReadOnlyList<ISkillCheckHost> All => _hosts;

        public void Add(ISkillCheckHost host)
        {
            if (!_hosts.Contains(host)) _hosts.Add(host);
        }

        public void Remove(ISkillCheckHost host) => _hosts.Remove(host);
    }
}
