#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// A client's view of one actor's gameplay tags, by name. Three sources combine:
    /// <list type="bullet">
    /// <item>the server's replicated set, which is the truth and reaches late joiners;</item>
    /// <item>the owner's optimistic adds and removes (tags it asked the server to change), which hold only until the
    /// server's set changes that tag, since that change is the server's answer;</item>
    /// <item>the owner's predicted effect tags, which the owner's own simulation adds and removes.</item>
    /// </list>
    /// </summary>
    public sealed class ClientTagState
    {
        private readonly HashSet<string> _replicated = new(StringComparer.Ordinal);
        private readonly HashSet<string> _optimisticAdds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _optimisticRemoves = new(StringComparer.Ordinal);
        private readonly HashSet<string> _predicted = new(StringComparer.Ordinal);
        private readonly List<string> _changed = new();

        public bool Has(string name) =>
            _predicted.Contains(name) ||
            _optimisticAdds.Contains(name) ||
            (_replicated.Contains(name) && !_optimisticRemoves.Contains(name));

        /// <summary>Replaces the server's set. Every tag whose presence changed drops its optimistic entry.</summary>
        public void SetReplicated(IEnumerable<string> names)
        {
            _changed.Clear();
            _changed.AddRange(_replicated);
            _replicated.Clear();
            foreach (var name in names)
            {
                if (!_replicated.Add(name)) continue;
                if (!_changed.Remove(name)) _changed.Add(name); // present before: unchanged; absent before: added
            }

            foreach (var name in _changed)
            {
                _optimisticAdds.Remove(name);
                _optimisticRemoves.Remove(name);
            }
        }

        public void AddOptimistic(string name)
        {
            _optimisticRemoves.Remove(name);
            _optimisticAdds.Add(name);
        }

        public void RemoveOptimistic(string name)
        {
            _optimisticAdds.Remove(name);
            _optimisticRemoves.Add(name);
        }

        public void AddPredicted(string name) => _predicted.Add(name);

        public void RemovePredicted(string name) => _predicted.Remove(name);

        public void Clear()
        {
            _replicated.Clear();
            _optimisticAdds.Clear();
            _optimisticRemoves.Clear();
            _predicted.Clear();
        }
    }
}
