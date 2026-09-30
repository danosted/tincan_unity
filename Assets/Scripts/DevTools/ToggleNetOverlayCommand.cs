#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.DevTools
{
    /// <summary>Show or hide the net harness readout. Handled by <see cref="ToggleNetOverlayInputHandler"/>.</summary>
    [CreateAssetMenu(fileName = "Command_ToggleNetOverlay", menuName = "TinCan/Input/Commands/Toggle Net Overlay")]
    public sealed class ToggleNetOverlayCommand : InputCommand { }
}
