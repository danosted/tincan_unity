#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// A client's view of one actor's gameplay tags, by name. Two sources combine:
    /// <list type="bullet">
    /// <item>the server's replicated set, which is the truth and reaches late joiners;</item>
    /// <item>the owner's predicted effect tags, which the owner's own simulation adds and removes.</item>
    /// </list>
    /// Clients never write tags themselves; only effects and the server change them.
    /// Queries match like the server's <see cref="GameplayTagContainer"/>: a held tag matches the query tag or any of
    /// its parents.
    /// </summary>
    public sealed class ClientTagState
    {
        private readonly HashSet<string> _replicated = new(StringComparer.Ordinal);
        private readonly HashSet<string> _predicted = new(StringComparer.Ordinal);

        public bool Has(string name) => _predicted.Contains(name) || _replicated.Contains(name);

        /// <summary>
        /// True when a held tag is <paramref name="query"/> or its child (<see cref="GameplayTag.IsChildOf"/>), the server's rule.
        /// Held tags are known by name, so they resolve through <paramref name="registry"/>; without one, only the exact
        /// name matches.
        /// </summary>
        public bool Has(GameplayTag query, IGameplayTagRegistry? registry)
        {
            if (Has(query.name)) return true;
            if (registry == null) return false;

            return AnyChildOf(_predicted, query, registry) || AnyChildOf(_replicated, query, registry);
        }

        private static bool AnyChildOf(HashSet<string> names, GameplayTag query, IGameplayTagRegistry registry)
        {
            foreach (var name in names)
            {
                if (registry.TryGet(name, out var held) && held.IsChildOf(query)) return true;
            }
            return false;
        }

        /// <summary>Replaces the server's set.</summary>
        public void SetReplicated(IEnumerable<string> names)
        {
            _replicated.Clear();
            _replicated.UnionWith(names);
        }

        public void AddPredicted(string name) => _predicted.Add(name);

        public void RemovePredicted(string name) => _predicted.Remove(name);

        public void Clear()
        {
            _replicated.Clear();
            _predicted.Clear();
        }
    }
}
