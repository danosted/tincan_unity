#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>An <see cref="IInputReader"/> whose actions the test sets directly; nothing is gated by contexts.</summary>
    public sealed class FakeInputReader : IInputReader
    {
        public HashSet<InputActionId> Pressed { get; } = new();
        public HashSet<InputActionId> Triggered { get; } = new();
        public Dictionary<InputActionId, float> Axes { get; } = new();
        public Dictionary<InputActionId, Vector2> Vectors { get; } = new();
        public ulong Mask { get; set; }

        public bool IsPressed(InputActionId? action) => action != null && Pressed.Contains(action);
        public bool WasPressedThisFrame(InputActionId? action) => action != null && Triggered.Contains(action);
        public float ReadAxis(InputActionId? action) => action != null && Axes.TryGetValue(action, out var value) ? value : 0f;
        public Vector2 ReadVector2(InputActionId? action) => action != null && Vectors.TryGetValue(action, out var value) ? value : Vector2.zero;
        public ulong GameplayInputMask() => Mask;
    }
}
