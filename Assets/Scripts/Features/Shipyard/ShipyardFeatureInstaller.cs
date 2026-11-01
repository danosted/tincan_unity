#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard: build a ship design solo and offline, save and load it, launch a session flying it. Adds a
    /// "Shipyard" row to the main menu and the Shipyard input context. Needs ShipDesigns. Plan:
    /// .docs/plans/modular-airship-builder.md (S3).
    /// </summary>
    [CreateAssetMenu(fileName = "ShipyardFeatureInstaller", menuName = "TinCan/Features/Shipyard Feature Installer")]
    public class ShipyardFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<InputContext>, FeatureInstaller.IExtension<StyleSheet>,
        IMainMenuRows
    {
        [SerializeField] private ShipyardConfig? _config;
        [Tooltip("Assets/Input/Contexts/Context_Shipyard: building while the shipyard is open.")]
        [SerializeField] private ShipyardInputContext? _controls;
        [Tooltip("The shipyard's UI component sheets (Features/Shipyard/UI/*.uss), added to the screen UI where the shipyard loads.")]
        [SerializeField] private List<StyleSheet> _styles = new();

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null || _controls == null)
            {
                Debug.LogWarning($"[{name}] No ShipyardConfig or Shipyard context assigned; there is no shipyard.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.RegisterInstance(_controls);
            builder.Register<ShipyardStage>(Lifetime.Singleton).As<IShipyardStage>();
            builder.Register<ShipyardUseCase>(Lifetime.Singleton).As<IShipyard>().As<ITickable>();
            builder.Register<ShipyardMenuInputHandler>(Lifetime.Singleton).As<IInputCommandHandler>();

            builder.Register<EnterShipyardMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ShipyardSaveMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ShipyardLoadMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ShipyardNewMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ShipyardLaunchMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<ShipyardExitMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
        }

        IEnumerable<InputContext> FeatureInstaller.IExtension<InputContext>.Contributions
        {
            get { if (_controls != null) yield return _controls; }
        }

        IEnumerable<StyleSheet> FeatureInstaller.IExtension<StyleSheet>.Contributions => _styles;

        public IEnumerable<MenuItemDefinition> MainMenuRows
        {
            get
            {
                if (_config == null || _controls == null) yield break;
                yield return new MenuItemDefinition
                {
                    ItemId = "shipyard", Label = "Shipyard", Kind = MenuItemKind.Command, CommandId = EnterShipyardMenuCommand.Id,
                };
            }
        }
    }
}
