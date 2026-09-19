#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Features
{
    /// <summary>Explicit allow-list of installers for a scene, in place of loading every installer in the project.</summary>
    [CreateAssetMenu(fileName = "FeatureProfile", menuName = "TinCan/Features/Feature Profile")]
    public sealed class FeatureProfile : ScriptableObject
    {
        [SerializeField] private List<FeatureProfile> _includes = new();
        [SerializeField] private List<FeatureInstaller> _installers = new();

        public IReadOnlyList<FeatureInstaller> Installers => _installers;

        /// <summary>This profile's own installers plus every included profile's, recursively, de-duplicated.</summary>
        public IEnumerable<FeatureInstaller> ResolveInstallers()
        {
            var result = new List<FeatureInstaller>();
            Collect(this, new HashSet<FeatureProfile>(), new HashSet<FeatureInstaller>(), result);
            return result;
        }

        private static void Collect(FeatureProfile? profile, HashSet<FeatureProfile> seenProfiles, HashSet<FeatureInstaller> seenInstallers, List<FeatureInstaller> result)
        {
            if (profile == null || !seenProfiles.Add(profile)) return; // guards against cycles and repeated includes

            foreach (var include in profile._includes) Collect(include, seenProfiles, seenInstallers, result);
            foreach (var installer in profile._installers)
                if (installer != null && seenInstallers.Add(installer)) result.Add(installer);
        }
    }
}
