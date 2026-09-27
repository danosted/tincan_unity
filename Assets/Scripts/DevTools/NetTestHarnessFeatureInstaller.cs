#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Features;
using TinCan.DevTools.Scenarios;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.DevTools
{
    /// <summary>
    /// Network test harness: simulated latency (<c>-netsim</c>), a scripted input bot (<c>-bot</c>), movement
    /// telemetry (<c>-telemetry</c>) and feature scenarios with pass/fail reports (<c>-scenario</c>). Flags come from the command line or MPPM player tags. With no flag it registers
    /// nothing, so normal play is unaffected. See .docs/NETWORK_TEST_HARNESS.md.
    /// </summary>
    [CreateAssetMenu(fileName = "NetTestHarnessFeatureInstaller", menuName = "TinCan/Features/Net Test Harness Installer")]
    public class NetTestHarnessFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            var options = HarnessOptions.Parse(LaunchArguments.Current);
            if (!options.IsActive) return;

            Debug.Log($"[NetHarness] Active: netsim={options.NetworkPreset ?? "none"}, bot={options.BotRoute ?? "none"}, " +
                      $"telemetry={options.TelemetryEnabled}, scenario={options.Scenario ?? "none"}{(options.ScenarioSolo ? " (solo)" : string.Empty)}.");

            builder.RegisterInstance(options);
            builder.Register<HarnessSession>(Lifetime.Singleton);
            builder.Register<NetworkConditionsUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>();
            builder.Register<BotRouteUseCase>(Lifetime.Singleton).As<ITickable>();
            builder.Register<HarnessGameplayOverrides>(Lifetime.Singleton).As<IInitializable>();
            builder.Register<MovementTelemetryUseCase>(Lifetime.Singleton).As<IInitializable>().As<IPostLateTickable>();
            InstallScenario(builder, options);
        }

        private static void InstallScenario(IContainerBuilder builder, HarnessOptions options)
        {
            if (options.Scenario == null) return;

            if (!ScenarioCatalog.TryGet(options.Scenario, out var entry))
            {
                Debug.LogError($"[Scenario] Unknown scenario '{options.Scenario}'. Known: {ScenarioCatalog.Names}.");
                return;
            }

            builder.RegisterInstance(entry);
            builder.Register<ScenarioTimeline>(Lifetime.Singleton);
            builder.Register<ScenarioSubject>(Lifetime.Singleton);
            builder.Register<CommonScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            builder.Register<GameplayCueScenarioLibrary>(Lifetime.Singleton).As<IScenarioLibrary>();
            entry.RegisterLibraries(builder);
            builder.Register<ScenarioEventRecorder>(Lifetime.Singleton).As<IEventObserver>();
            builder.Register<ScenarioUseCase>(Lifetime.Singleton).As<ITickable>();
        }
    }
}
