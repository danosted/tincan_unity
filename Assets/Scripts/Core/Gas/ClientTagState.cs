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
    /// <para>
    /// A predicted tag the owner ends before the server has shown it (a pull shorter than the round trip) is "ended
    /// ahead": the server's late copy of it is the echo of that same press, so it is ignored until the server drops it
    /// again, or until the owner predicts the tag anew. Without this the tag (and every cue that follows it) would come
    /// back on for a moment after the owner let go.
    /// </para>
    /// </summary>
    public sealed class ClientTagState
    {
        private readonly HashSet<string> _replicated = new(StringComparer.Ordinal);
        private readonly HashSet<string> _predicted = new(StringComparer.Ordinal);
        // Predicted tags ended before the server showed them, and of those, the ones whose late echo has arrived.
        private readonly HashSet<string> _endedAhead = new(StringComparer.Ordinal);
        private readonly HashSet<string> _echoSeen = new(StringComparer.Ordinal);
        private readonly List<string> _scratch = new();

        public bool Has(string name) => _predicted.Contains(name) || (_replicated.Contains(name) && !_endedAhead.Contains(name));

        /// <summary>
        /// True when a held tag is <paramref name="query"/> or its child (<see cref="GameplayTag.IsChildOf"/>), the server's rule.
        /// Held tags are known by name, so they resolve through <paramref name="registry"/>; without one, only the exact
        /// name matches.
        /// </summary>
        public bool Has(GameplayTag query, IGameplayTagRegistry? registry)
        {
            if (Has(query.name)) return true;
            if (registry == null) return false;

            return AnyChildOf(_predicted, query, registry, ignore: null) || AnyChildOf(_replicated, query, registry, _endedAhead);
        }

        private static bool AnyChildOf(HashSet<string> names, GameplayTag query, IGameplayTagRegistry registry, HashSet<string>? ignore)
        {
            foreach (var name in names)
            {
                if (ignore != null && ignore.Contains(name)) continue;
                if (registry.TryGet(name, out var held) && held.IsChildOf(query)) return true;
            }
            return false;
        }

        /// <summary>Replaces the server's set. An ended-ahead tag's echo is ignored while it lasts and forgotten once it is gone.</summary>
        public void SetReplicated(IEnumerable<string> names)
        {
            _replicated.Clear();
            _replicated.UnionWith(names);

            _scratch.Clear();
            _scratch.AddRange(_endedAhead);
            foreach (var name in _scratch)
            {
                if (_replicated.Contains(name)) _echoSeen.Add(name);
                else if (_echoSeen.Remove(name)) _endedAhead.Remove(name);
            }
        }

        public void AddPredicted(string name)
        {
            _predicted.Add(name);
            // A new press: the server's copy from here on belongs to it.
            _endedAhead.Remove(name);
            _echoSeen.Remove(name);
        }

        public void RemovePredicted(string name)
        {
            if (!_predicted.Remove(name)) return;
            // Ended before the server showed it: its copy, when it comes, is late.
            if (!_replicated.Contains(name)) _endedAhead.Add(name);
        }

        public void Clear()
        {
            _replicated.Clear();
            _predicted.Clear();
            _endedAhead.Clear();
            _echoSeen.Clear();
        }
    }
}
