#nullable enable
using TinCan.Core.Domain.Input;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Core input: the actions, the contexts, the reader every system reads through, the router that turns discrete
    /// actions into commands, and scripted input for bots and scenarios. Each core context is also registered as its own
    /// type, so a system asks for the context it listens to (<c>HumanoidInputContext</c>) in its constructor; a feature
    /// registers and contributes its own (<c>FeatureInstaller.IExtension&lt;InputContext&gt;</c>). Handlers for the
    /// routed commands are registered by the systems that own the commands. Model: .docs/INPUT.md.
    /// </summary>
    [CreateAssetMenu(fileName = "InputFeatureInstaller", menuName = "TinCan/Features/Input Feature Installer")]
    public class InputFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Assets/Input/InputConfig.asset.")]
        [SerializeField] private InputConfig? _config;

        public override int Order => -30;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogError($"[{name}] No InputConfig assigned; the game cannot read input.", this);
                return;
            }

            builder.RegisterInstance(_config);
            foreach (var context in _config.Contexts)
            {
                // Plain contexts (Menu, Rebinding) only route commands; nobody asks for them by type.
                if (context == null || context.GetType() == typeof(InputContext)) continue;
                builder.RegisterInstance(context, context.GetType());
            }

            var config = _config;
            builder.Register(resolver => InputContextSet.From(config, resolver.ResolveOrDefault<FeatureInstallerCatalog>()), Lifetime.Singleton);

            builder.Register<ScriptedInput>(Lifetime.Singleton).AsSelf().As<IScriptedInput>().As<ILateTickable>();
            builder.Register<InputSystemReader>(Lifetime.Singleton).AsSelf().As<IInputReader>().As<IInputActionSwitch>().As<IInitializable>();
            builder.Register<InputRebindState>(Lifetime.Singleton);
            builder.Register<InputContextConditions>(Lifetime.Singleton).As<IInputContextConditions>();
            builder.Register<InputContextProcessor>(Lifetime.Transient);
            builder.Register<InputContextUseCase>(Lifetime.Singleton).As<IInputContexts>().As<IInitializable>().As<ITickable>();
            builder.Register<InputRoutingUseCase>(Lifetime.Singleton).AsSelf().As<ITickable>();

            // The player's key bindings (Controls menu).
            builder.Register<PlayerPrefsInputBindingStore>(Lifetime.Singleton).As<IInputBindingStore>();
            builder.Register<InputBindingConflictProcessor>(Lifetime.Transient);
            builder.Register<InputRebindingUseCase>(Lifetime.Singleton).AsSelf().As<IInputBindings>().As<IInitializable>();
        }
    }
}
