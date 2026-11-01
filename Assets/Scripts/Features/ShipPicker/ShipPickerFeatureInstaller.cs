#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.UI;
using UnityEngine;
using VContainer;

namespace TinCan.Features.ShipPicker
{
    /// <summary>
    /// Temporary: a "Change ship" row in the main and pause menu that lists every stored design; the host picks one and the
    /// ships in play rebuild as it. Needs ShipDesigns. To remove it, see .docs/plans/ship-picker-temporary.md, "Removing it".
    /// </summary>
    [CreateAssetMenu(fileName = "ShipPickerFeatureInstaller", menuName = "TinCan/Features/Ship Picker Feature Installer")]
    public class ShipPickerFeatureInstaller : FeatureInstaller, IMainMenuRows
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<ShipPickerUseCase>(Lifetime.Singleton).As<IShipPicker>();
            builder.Register<OpenShipPickerMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
            builder.Register<PickShipMenuCommand>(Lifetime.Singleton).As<IMenuCommand>();
        }

        public IEnumerable<MenuItemDefinition> MainMenuRows
        {
            get
            {
                yield return new MenuItemDefinition
                {
                    ItemId = "change-ship", Label = "Change ship", Kind = MenuItemKind.Command, CommandId = OpenShipPickerMenuCommand.Id,
                };
            }
        }
    }
}
