#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Core.UI
{
    /// <summary>
    /// The Menu context's slots: Navigate (arrows, d-pad, left stick) and NavigateLetters (WASD) move the focused row and
    /// adjust it, Submit (Enter, gamepad South) uses it. Read by <see cref="MenuNavigationUseCase"/>; Cancel is routed to
    /// MenuBackInputHandler. The letters are a separate action because they are also text: on a text field they type.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Menu", menuName = "TinCan/Input/Contexts/Menu Context")]
    public sealed class MenuInputContext : InputContext
    {
        [Tooltip("Up and down move the focus between rows; left and right adjust a slider, choice or toggle.")]
        public InputActionId? Navigate;
        [Tooltip("WASD, the same as Navigate except on a text field, where the letters are typed instead.")]
        public InputActionId? NavigateLetters;
        [Tooltip("Use the focused row.")]
        public InputActionId? Submit;

        protected override IEnumerable<InputActionId?> Slots => new[] { Navigate, NavigateLetters, Submit };
    }
}
