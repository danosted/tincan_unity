#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.SkillChecks
{
    /// <summary>
    /// Doing a skill check: open (<see cref="InputContextActivation.WhileOpened"/>) while <see cref="SkillCheckRunner"/>
    /// runs one on this peer. It silences walking and looking, so the keys play the check; Press grades it.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_SkillCheck", menuName = "TinCan/Input/Skill Check Context")]
    public sealed class SkillCheckInputContext : InputContext
    {
        [Tooltip("Press while the needle is in the band (button).")]
        public InputActionId? Press;

        protected override IEnumerable<InputActionId?> Slots => new[] { Press };
    }
}
