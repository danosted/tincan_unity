#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Building in the shipyard: live while the shipyard is open (<see cref="InputContextActivation.WhileOpened"/>), and it
    /// silences everything below it. <see cref="ShipyardUseCase"/> reads the slots; Cancel is routed to
    /// <see cref="OpenShipyardMenuCommand"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Shipyard", menuName = "TinCan/Input/Shipyard Context")]
    public sealed class ShipyardInputContext : InputContext
    {
        [Tooltip("The cursor's position on screen (pixels).")]
        public InputActionId? Point;
        [Tooltip("Mouse movement while orbiting.")]
        public InputActionId? Orbit;
        [Tooltip("Held to orbit the camera around the ship.")]
        public InputActionId? OrbitHold;
        [Tooltip("Zoom in and out (scroll).")]
        public InputActionId? Zoom;
        [Tooltip("Place the selected part where the cursor points.")]
        public InputActionId? Place;
        [Tooltip("Remove the part under the cursor.")]
        public InputActionId? Remove;
        [Tooltip("Turn the selected part a quarter turn.")]
        public InputActionId? Rotate;
        public InputActionId? NextPart;
        public InputActionId? PreviousPart;
        public InputActionId? Undo;
        public InputActionId? Redo;

        protected override IEnumerable<InputActionId?> Slots => new[]
        {
            Point, Orbit, OrbitHold, Zoom, Place, Remove, Rotate, NextPart, PreviousPart, Undo, Redo,
        };
    }
}
