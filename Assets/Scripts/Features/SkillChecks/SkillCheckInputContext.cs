#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Doing a skill check: open (<see cref="InputContextActivation.WhileOpened"/>) while <see cref="SkillCheckRunner"/>
    /// runs one on this peer. It silences walking and looking, so the keys play the check: Press grades the pulsing
    /// gauge, the four directions play the button combo.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_SkillCheck", menuName = "TinCan/Input/Skill Check Context")]
    public sealed class SkillCheckInputContext : InputContext
    {
        [Tooltip("Press while the needle is in the band (button).")]
        public InputActionId? Press;
        [Tooltip("The combo's directions (buttons).")]
        public InputActionId? Left;
        public InputActionId? Right;
        public InputActionId? Up;
        public InputActionId? Down;

        protected override IEnumerable<InputActionId?> Slots => new[] { Press, Left, Right, Up, Down };

        /// <summary>The action for a combo direction.</summary>
        public InputActionId? For(ComboDirection direction) => direction switch
        {
            ComboDirection.Left => Left,
            ComboDirection.Right => Right,
            ComboDirection.Up => Up,
            _ => Down
        };
    }
}
