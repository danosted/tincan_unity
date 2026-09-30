#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI.Commands;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain.Hud;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Headless menu/HUD framework plus the Start Host / Join menu. See .docs/UI_FRAMEWORK.md.
    /// Views are optional MonoBehaviours implementing IInjectedView (injected by the lifetime scope).
    /// </summary>
    [CreateAssetMenu(fileName = "UiFeatureInstaller", menuName = "TinCan/Features/UI Feature Installer")]
    public class UiFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private MenuDefinition? _mainMenu;

        public override int Order => -10;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<MenuCommandRegistry>(Lifetime.Singleton).As<IMenuCommandRegistry>();
            builder.Register<MenuUseCase>(Lifetime.Singleton).As<IMenuSystem>();
            builder.Register<CommandLineSessionBootstrap>(Lifetime.Singleton).As<IStartable>().As<ITickable>();
            builder.Register<HudUseCase>(Lifetime.Singleton).As<IHudValues>();
            builder.Register<StartHostMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<JoinGameMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<QuitMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ResetBindingsMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();

            if (_mainMenu == null)
            {
                Debug.LogWarning($"[{name}] No main MenuDefinition assigned; the start menu will not appear.", this);
                return;
            }

            builder.RegisterInstance(_mainMenu);
            builder.Register<MainMenuBootstrap>(Lifetime.Singleton).As<IInitializable>().As<ITickable>();

            // Cancel, routed by the Menu and Global input contexts.
            builder.Register<MenuBackInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();
            builder.Register<OpenMenuInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();
        }
    }
}
