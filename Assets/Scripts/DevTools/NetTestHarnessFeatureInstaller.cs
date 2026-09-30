#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Input;
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
    public class NetTestHarnessFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<InputContext>
    {
        [Tooltip("Assets/Input/Contexts/Context_DevTools: F3 toggles the readout. Contributed only while the harness runs.")]
        [SerializeField] private InputContext? _controls;

        public IEnumerable<InputContext> Contributions
        {
            get { if (_controls != null && HarnessOptions.Parse(LaunchArguments.Current).IsActive) yield return _controls; }
        }

        public override void Install(IContainerBuilder builder)
        {
            var options = HarnessOptions.Parse(LaunchArguments.Current);
            if (!options.IsActive) return;

            Debug.Log($"[NetHarness] Active: netsim={options.NetworkPreset ?? "none"}, bot={options.BotRoute ?? "none"}, " +
                      $"telemetry={options.TelemetryEnabled}, scenario={options.Scenario ?? "none"}{(options.ScenarioSolo ? " (solo)" : string.Empty)}.");

            builder.RegisterInstance(options);
            builder.Register<HarnessSession>(Lifetime.Singleton);
            builder.Register<ScriptedActionMap>(Lifetime.Singleton);
            builder.Register<ScriptedActionDriver>(Lifetime.Singleton);
            builder.Register<NetworkConditionsUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>();
            builder.Register<BotRouteUseCase>(Lifetime.Singleton).As<ITickable>();
            builder.Register<HarnessGameplayOverrides>(Lifetime.Singleton).As<IInitializable>();
            builder.Register<MovementTelemetryUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>().As<IPostLateTickable>();
            builder.Register<ToggleNetOverlayInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();
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

            // A scenario's libraries need the features its own scene loads; in another scene they cannot be built.
            // This happens with a scenario player tag left over from an interrupted run.
            var scene = builder.ApplicationOrigin is Component scope ? scope.gameObject.scene.path : null;
            var expected = entry.Scenario.ScenePath;
            if (expected != null && !string.IsNullOrEmpty(scene) && scene != expected)
            {
                Debug.LogWarning($"[Scenario] '{options.Scenario}' runs in {expected}, not {scene}; not starting it. " +
                                 "Leftover scenario player tag? Clear it in Window > Multiplayer > Multiplayer Play Mode.");
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
