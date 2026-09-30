#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// Actions that mean something whatever the player controls: Cancel and switching what you possess. Always active,
    /// lowest priority, so every other context gets a Cancel first (the menu steps back, the airship lets go) and this
    /// one opens the main menu only when nobody else wanted it.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Global", menuName = "TinCan/Input/Global Context")]
    public sealed class GlobalInputContext : InputContext
    {
        [Tooltip("Back out: close the menu, leave the helm, or open the main menu.")]
        public InputActionId? Cancel;
        [Tooltip("Cycle to the next thing this player may control (Tab).")]
        public InputActionId? SwitchPossession;

        protected override IEnumerable<InputActionId?> Slots => new[] { Cancel, SwitchPossession };
    }
}
