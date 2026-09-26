#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Network test harness: simulated latency (<c>-netsim</c>), a scripted input bot (<c>-bot</c>) and movement
    /// telemetry (<c>-telemetry</c>). Flags come from the command line or MPPM player tags. With no flag it registers
    /// nothing, so normal play is unaffected. See .docs/NETWORK_TEST_HARNESS.md.
    /// </summary>
    [CreateAssetMenu(fileName = "NetTestHarnessFeatureInstaller", menuName = "TinCan/Features/Net Test Harness Installer")]
    public class NetTestHarnessFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            var options = HarnessOptions.Parse(LaunchArguments.Current);
            if (!options.IsActive) return;

            Debug.Log($"[NetHarness] Active: netsim={options.NetworkPreset ?? "none"}, bot={options.BotRoute ?? "none"}, telemetry={options.TelemetryEnabled}.");

            builder.RegisterInstance(options);
            builder.Register<HarnessSession>(Lifetime.Singleton);
            builder.Register<NetworkConditionsUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>();
            builder.Register<BotRouteUseCase>(Lifetime.Singleton).As<ITickable>();
            builder.Register<HarnessGameplayOverrides>(Lifetime.Singleton).As<IInitializable>();
            builder.Register<MovementTelemetryUseCase>(Lifetime.Singleton).As<IInitializable>().As<IPostLateTickable>();
        }
    }
}
