#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.CloudFlight
{
    /// <summary>
    /// The cloud flight (plan .docs/plans/cloud-sea.md, "Soaring and cloud shelves"): dive through the cloud to build
    /// speed, launch out of it, soar and glide home. Forces on the ships' movement (<see cref="IAirshipFlightForces"/>);
    /// without this installer ships fly on their engine alone. Needs the cloud boundary (its surface query). Also the
    /// developer dashboard (<see cref="CloudFlightDashboard"/>). Built by TinCan > Dev > Cloud Flight > Build Assets.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudFlightFeatureInstaller", menuName = "TinCan/Features/Cloud Flight Feature Installer")]
    public class CloudFlightFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<StyleSheet>
    {
        [SerializeField] private CloudFlightConfig? _config;
        [Tooltip("Features/CloudFlight/CloudFlightDashboard.uss: the developer dashboard's look.")]
        [SerializeField] private StyleSheet? _dashboardStyle;

        IEnumerable<StyleSheet> FeatureInstaller.IExtension<StyleSheet>.Contributions
        {
            get { if (_dashboardStyle != null) yield return _dashboardStyle; }
        }

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No CloudFlightConfig assigned; ships fly on their engine alone.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<CloudFlightProcessor>(Lifetime.Singleton);
            builder.Register<CloudFlightUseCase>(Lifetime.Singleton).As<IAirshipFlightForces>().AsSelf();
            builder.RegisterEntryPoint<CloudFlightDashboard>();
        }
    }
}
