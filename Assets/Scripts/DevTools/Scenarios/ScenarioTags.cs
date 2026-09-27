#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Finds gameplay tag assets by name for scenario arguments: through <see cref="IGameplayTagRegistry"/> when the
    /// gameplay tags installer is active, otherwise by scanning loaded assets.
    /// </summary>
    public static class ScenarioTags
    {
        private static readonly Dictionary<string, GameplayTag> Cache = new(StringComparer.Ordinal);

        public static GameplayTag? Find(string name, IGameplayTagRegistry? registry)
        {
            if (registry != null) return registry.TryGet(name, out var registered) ? registered : null;

            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;

            var tag = Resources.FindObjectsOfTypeAll<GameplayTag>().FirstOrDefault(candidate => candidate.name == name);
            if (tag != null) Cache[name] = tag;
            return tag;
        }
    }
}
