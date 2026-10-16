#nullable enable
using System.Linq;
using TinCan.Core.UI;

namespace TinCan.Features.Respawn
{
    /// <summary>The overboard and leash menus, built in code from the rules (no menu asset to keep in step).</summary>
    public static class OverboardMenus
    {
        public const string OverboardMenuId = "Overboard";
        public const string LeashMenuId = "Leashed";
        public const string RespawnItem = "respawn";
        public const string LeashItem = "leash";

        public static MenuDefinition Overboard(RespawnRule rule, bool canLeash)
        {
            var rows = new[] { Respawn(rule) }.ToList();
            if (canLeash)
            {
                rows.Add(new MenuItemDefinition
                {
                    ItemId = LeashItem, Label = "Hang on the leash (keep what you carry)", Kind = MenuItemKind.Command,
                    CommandId = HangOnLeashMenuCommand.Id
                });
            }

            return MenuDefinition.Create(OverboardMenuId, "Overboard!", rows.ToArray());
        }

        public static MenuDefinition Leashed(RespawnRule rule) =>
            MenuDefinition.Create(LeashMenuId, "On the leash", Respawn(rule),
                new MenuItemDefinition { ItemId = "back", Label = "Keep hanging on", Kind = MenuItemKind.Back });

        private static MenuItemDefinition Respawn(RespawnRule rule) => new()
        {
            ItemId = RespawnItem,
            Label = rule.CarriedItem == CarriedItemOutcome.Lose ? "Respawn aboard (lose what you carry)" : "Respawn aboard",
            Kind = MenuItemKind.Command,
            CommandId = RespawnAboardMenuCommand.Id
        };
    }
}
