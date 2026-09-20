#nullable enable
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Client-side atmosphere: dims sunlight and blends in whiteout fog as the local camera
    /// (airship or on-foot) submerges into the cloud deck. No gameplay effect, no networking.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudSubmersionFeatureInstaller", menuName = "TinCan/Features/Cloud Submersion Feature Installer")]
    public class CloudSubmersionFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private CloudSubmersionConfig _config = null!;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_config);
            builder.Register<CloudSubmersionProcessor>(Lifetime.Singleton);
        }
    }
}
