#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Settings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Core.Settings
{
    /// <summary>
    /// Player settings (core: loads in every scene). Registers <see cref="IPlayerSettings"/> over the core settings listed
    /// here plus those the loaded features contribute. With <c>-defaultsettings</c> (every harness run) nothing is read or
    /// saved. See .docs/plans/player-settings.md.
    /// </summary>
    [CreateAssetMenu(fileName = "SettingsFeatureInstaller", menuName = "TinCan/Features/Settings Feature Installer")]
    public class SettingsFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Settings every scene has (Assets/Settings/PlayerSettings): look sensitivity, invert Y.")]
        [SerializeField] private List<SettingDefinition> _settings = new();

        public override int Order => -20;

        public override void Install(IContainerBuilder builder)
        {
            var core = _settings;
            builder.Register(resolver => SettingsCatalog.From(core, resolver.ResolveOrDefault<FeatureInstallerCatalog>()), Lifetime.Singleton);

            if (LaunchArguments.HasFlag(LaunchArguments.Current, DefaultsOnlySettingsStore.Flag))
                builder.Register<DefaultsOnlySettingsStore>(Lifetime.Singleton).As<ISettingsStore>();
            else
                builder.Register<PlayerPrefsSettingsStore>(Lifetime.Singleton).As<ISettingsStore>();

            builder.Register<PlayerSettingsUseCase>(Lifetime.Singleton).As<IPlayerSettings>().As<ITickable>();
        }
    }
}
