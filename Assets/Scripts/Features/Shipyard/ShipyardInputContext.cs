#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Building in the shipyard: live while the shipyard is open (<see cref="InputContextActivation.WhileOpened"/>), and it
    /// silences everything below it. <see cref="ShipyardUseCase"/> reads the slots; Cancel is routed to
    /// <see cref="OpenShipyardMenuCommand"/>. The camera flies (WASD, Q/E, Shift; look while the right button is held),
    /// the cursor builds. Plan: .docs/plans/ship-frames.md (S2).
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Shipyard", menuName = "TinCan/Input/Shipyard Context")]
    public sealed class ShipyardInputContext : InputContext
    {
        [Tooltip("The cursor's position on screen (pixels).")]
        public InputActionId? Point;
        [Tooltip("Mouse movement: turns the camera while LookHold is held.")]
        public InputActionId? Look;
        [Tooltip("Held to look around with the mouse.")]
        public InputActionId? LookHold;
        [Tooltip("Fly the camera along the view (WASD).")]
        public InputActionId? Fly;
        [Tooltip("Fly the camera down and up (Q/E).")]
        public InputActionId? Rise;
        [Tooltip("Held to fly faster.")]
        public InputActionId? Fast;
        [Tooltip("Move the camera straight forward and back (scroll).")]
        public InputActionId? Zoom;
        [Tooltip("Parts mode: place the selected part (in delete mode: delete). Frame mode: pick, add, join, drag nodes.")]
        public InputActionId? Place;
        [Tooltip("Toggle delete mode: what is under the cursor is highlighted, and Place deletes it.")]
        public InputActionId? DeleteMode;
        [Tooltip("Turn the selected part a quarter turn.")]
        public InputActionId? Rotate;
        [Tooltip("Switch between building with parts and drawing the frame.")]
        public InputActionId? Mode;
        [Tooltip("Frame mode: plank or open the face under the cursor.")]
        public InputActionId? Fill;
        [Tooltip("Frame mode: held while dragging a node, the drag raises or lowers it instead of moving it.")]
        public InputActionId? Height;
        [Tooltip("Frame mode: extrude what is under the cursor: push a wall out, grow a floor edge, raise a story on a floor.")]
        public InputActionId? Extrude;
        [Tooltip("Frame mode: a railing, a gate or nothing along the floor edge under the cursor, in turn.")]
        public InputActionId? Railing;
        [Tooltip("Frame mode: stairs from the floor edge under the cursor down to the story below; again turns them round, then takes them away.")]
        public InputActionId? Stairs;
        public InputActionId? Undo;
        public InputActionId? Redo;
        [Tooltip("Frame mode: the working story up.")]
        public InputActionId? LevelUp;
        public InputActionId? LevelDown;

        protected override IEnumerable<InputActionId?> Slots => new[]
        {
            Point, Look, LookHold, Fly, Rise, Fast, Zoom, Place, DeleteMode, Rotate, Mode, Fill, Height, Extrude, Railing, Stairs, Undo, Redo, LevelUp,
            LevelDown,
        };
    }
}
