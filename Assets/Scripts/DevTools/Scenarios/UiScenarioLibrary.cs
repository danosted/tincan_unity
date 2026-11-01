#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.UI;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Probes for what this peer's screen shows, read from the UI model: the menu on top (its id, or "none"), whether the
    /// HUD shows behind it (<see cref="MenuBackdropRules"/>), and the cursor (<see cref="ICursorPolicy"/>). Every scenario
    /// can use them; press Esc with <c>Tap(ScriptedAction.Cancel)</c>.
    /// </summary>
    public sealed class UiScenarioLibrary : IScenarioLibrary
    {
        private readonly IMenuSystem _menus;
        private readonly ICursorPolicy _cursor;

        public UiScenarioLibrary(IMenuSystem menus, ICursorPolicy cursor)
        {
            _menus = menus;
            _cursor = cursor;
        }

        public IEnumerable<ScenarioCommand> Commands => Array.Empty<ScenarioCommand>();

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("TopMenu", CheckTopMenu),
            new ScenarioProbe("HudShown", _ => CheckHud(expected: true)),
            new ScenarioProbe("HudHidden", _ => CheckHud(expected: false)),
            new ScenarioProbe("CursorLocked", _ => CheckCursor(CursorState.Locked)),
            new ScenarioProbe("CursorFree", _ => CheckCursor(CursorState.Free))
        };

        private ScenarioCheck CheckTopMenu(string expected)
        {
            string shown = _menus.Current?.MenuId ?? "none";
            string detail = _menus.Current == null ? "none" : $"{shown} ({_menus.Current.Owner}, {_menus.Current.Kind}{(_menus.Current.Pinned ? ", pinned" : "")})";
            return string.Equals(shown, expected, StringComparison.OrdinalIgnoreCase)
                ? ScenarioCheck.Pass($"top menu {detail}")
                : ScenarioCheck.Fail($"top menu {detail}, expected {expected}");
        }

        private ScenarioCheck CheckHud(bool expected)
        {
            bool shown = MenuBackdropRules.For(_menus.Current?.Kind).ShowHud;
            return shown == expected
                ? ScenarioCheck.Pass(shown ? "HUD shown" : "HUD hidden")
                : ScenarioCheck.Fail($"HUD {(shown ? "shown" : "hidden")} under {_menus.Current?.MenuId ?? "no menu"}");
        }

        private ScenarioCheck CheckCursor(CursorState expected) => _cursor.Mode == expected
            ? ScenarioCheck.Pass($"cursor {expected}")
            : ScenarioCheck.Fail($"cursor {_cursor.Mode}, expected {expected}");
    }
}
