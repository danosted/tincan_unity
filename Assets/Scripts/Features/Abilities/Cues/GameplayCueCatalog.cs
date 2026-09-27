#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// Every cue notify contributed by the active installers (<see cref="FeatureInstaller.IExtension{T}"/> for
    /// <see cref="GameplayCueNotify"/>), looked up by cue tag. A cue can have several notifies. Notifies without a cue
    /// tag are left out and reported in <see cref="Problems"/>.
    /// </summary>
    public sealed class GameplayCueCatalog
    {
        private readonly Dictionary<GameplayTag, List<GameplayCueNotify>> _byCue = new();
        private readonly List<string> _problems = new();

        public GameplayCueCatalog(FeatureInstallerCatalog installers)
        {
            var notifies = installers.Installers
                .OfType<FeatureInstaller.IExtension<GameplayCueNotify>>()
                .SelectMany(extension => extension.Contributions)
                .Where(notify => notify != null)
                .Distinct();

            foreach (var notify in notifies)
            {
                if (notify.Cue == null)
                {
                    _problems.Add($"{notify.name} has no cue tag; it is ignored.");
                    continue;
                }

                if (!_byCue.TryGetValue(notify.Cue, out var list)) _byCue[notify.Cue] = list = new List<GameplayCueNotify>();
                list.Add(notify);
            }
        }

        /// <summary>Every cue tag some notify presents.</summary>
        public IReadOnlyCollection<GameplayTag> Cues => _byCue.Keys;

        public IReadOnlyList<string> Problems => _problems;

        public IReadOnlyList<GameplayCueNotify> For(GameplayTag cue) =>
            _byCue.TryGetValue(cue, out var list) ? list : Array.Empty<GameplayCueNotify>();
    }
}
