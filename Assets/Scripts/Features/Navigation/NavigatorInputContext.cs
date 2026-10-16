#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// At the chart table: active while the local player's body carries State.Occupying.Chart (the occupy effect,
    /// replicated from the server). It blocks the Humanoid and Camera contexts, so the body stands still and the mouse
    /// moves the chart's cursor instead (<see cref="ChartNavigatorUseCase"/> reads it). Leave reaches the simulation as the
    /// Interact ability bit.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Navigator", menuName = "TinCan/Input/Navigator Context")]
    public sealed class NavigatorInputContext : InputContext
    {
        [Tooltip("Chart cursor delta (Vector2, mouse counts per frame).")]
        public InputActionId? Cursor;
        [Tooltip("Add a waypoint, pick a point of interest, or drag a waypoint while held (button).")]
        public InputActionId? Place;
        [Tooltip("Remove the waypoint under the cursor (button).")]
        public InputActionId? Remove;
        [Tooltip("Raise (+) or lower (-) the selected waypoint while held (axis).")]
        public InputActionId? Altitude;
        [Tooltip("Zoom the chart (axis; scroll).")]
        public InputActionId? Zoom;
        [Tooltip("Leave the chart table (button; the Interact ability input).")]
        public InputActionId? Leave;

        protected override IEnumerable<InputActionId?> Slots => new[] { Cursor, Place, Remove, Altitude, Zoom, Leave };
    }
}
