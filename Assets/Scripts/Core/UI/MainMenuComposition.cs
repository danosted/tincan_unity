#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.UI.Commands;

namespace TinCan.Core.UI
{
    /// <summary>
    /// The main menu as the scene loads it: the authored menu plus the rows loaded features add (<see cref="IMainMenuRows"/>),
    /// before the Quit row. With nothing added it is the authored asset itself.
    /// </summary>
    public static class MainMenuComposition
    {
        public static MenuDefinition Compose(MenuDefinition authored, IEnumerable<FeatureInstaller>? installers)
        {
            var added = (installers ?? Enumerable.Empty<FeatureInstaller>())
                .OfType<IMainMenuRows>()
                .SelectMany(i => i.MainMenuRows)
                .ToList();
            if (added.Count == 0) return authored;

            var rows = authored.Items.ToList();
            int quit = rows.FindIndex(r => r.Kind == MenuItemKind.Command && r.CommandId == QuitMenuCommand.Id);
            rows.InsertRange(quit >= 0 ? quit : rows.Count, added);

            var composed = MenuDefinition.Create(authored.MenuId, authored.Title, rows.ToArray());
            composed.name = authored.name;
            return composed;
        }
    }
}
