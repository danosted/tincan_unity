#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Name → tag lookup built once from <see cref="GameplayTagDatabase"/>. Tag names are asset names, which Unity
    /// keeps unique per folder only, so a duplicate name is reported and the first tag wins.
    /// </summary>
    public sealed class GameplayTagRegistry : IGameplayTagRegistry
    {
        private readonly Dictionary<string, GameplayTag> _byName = new(StringComparer.Ordinal);

        public GameplayTagRegistry(IEnumerable<GameplayTag?> tags)
        {
            var duplicates = new List<string>();
            foreach (var tag in tags)
            {
                if (tag == null) continue;
                if (!_byName.TryAdd(tag.name, tag)) duplicates.Add(tag.name);
            }

            Duplicates = duplicates.Distinct().ToArray();
        }

        public IReadOnlyCollection<GameplayTag> All => _byName.Values;

        /// <summary>Names that appeared more than once; lookups for them return the first tag registered.</summary>
        public IReadOnlyList<string> Duplicates { get; }

        public bool TryGet(string name, out GameplayTag tag) => _byName.TryGetValue(name, out tag!);
    }
}
