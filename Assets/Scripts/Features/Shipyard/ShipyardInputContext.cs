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
        [Tooltip("Place the selected part where the cursor points (in delete mode: delete the highlighted part).")]
        public InputActionId? Place;
        [Tooltip("Toggle delete mode: the part under the cursor is highlighted, and Place deletes it.")]
        public InputActionId? DeleteMode;
        [Tooltip("Turn the selected part a quarter turn.")]
        public InputActionId? Rotate;
        public InputActionId? NextPart;
        public InputActionId? PreviousPart;
        public InputActionId? Undo;
        public InputActionId? Redo;
        [Tooltip("Move the working level up (new parts go on it).")]
        public InputActionId? LevelUp;
        public InputActionId? LevelDown;

        protected override IEnumerable<InputActionId?> Slots => new[]
        {
            Point, Orbit, OrbitHold, Zoom, Place, DeleteMode, Rotate, NextPart, PreviousPart, Undo, Redo, LevelUp, LevelDown,
        };
    }
}
