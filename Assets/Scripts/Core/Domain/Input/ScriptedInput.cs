#nullable enable
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// The automation seam behind <see cref="IScriptedInput"/>. The input reader merges it with the devices, under the
    /// same context rules, so gameplay code cannot tell a bot from a keyboard.
    /// </summary>
    public sealed class ScriptedInput : IScriptedInput, ILateTickable
    {
        private readonly Dictionary<InputActionId, List<Vector2>> _held = new();
        private readonly Dictionary<InputActionId, int> _tapSeenFrame = new();
        private readonly List<InputActionId> _consumed = new();

        public void Press(InputActionId action) => Press(action, Vector2.right);

        public void Press(InputActionId action, Vector2 value)
        {
            if (!_held.TryGetValue(action, out var values)) _held[action] = values = new List<Vector2>();
            values.Add(value);
        }

        public void Release(InputActionId action) => _held.Remove(action);

        public void Release(InputActionId action, Vector2 value)
        {
            if (!_held.TryGetValue(action, out var values)) return;
            values.Remove(value);
            if (values.Count == 0) _held.Remove(action);
        }

        public void Tap(InputActionId action) => _tapSeenFrame[action] = -1;

        public void Clear()
        {
            _held.Clear();
            _tapSeenFrame.Clear();
        }

        public bool IsPressed(InputActionId action) => _held.ContainsKey(action);

        /// <summary>The sum of every held value (two held directions on one move action add up).</summary>
        public Vector2 Value(InputActionId action)
        {
            if (!_held.TryGetValue(action, out var values)) return Vector2.zero;
            var sum = Vector2.zero;
            foreach (var value in values) sum += value;
            return sum;
        }

        public bool WasTriggered(InputActionId action)
        {
            if (!_tapSeenFrame.ContainsKey(action)) return false;
            _tapSeenFrame[action] = Time.frameCount;
            return true;
        }

        // Runs in LateUpdate: a tap read during this frame is spent; unread taps wait for the next reader.
        public void LateTick()
        {
            _consumed.Clear();
            foreach (var pair in _tapSeenFrame)
            {
                if (pair.Value == Time.frameCount) _consumed.Add(pair.Key);
            }
            foreach (var key in _consumed) _tapSeenFrame.Remove(key);
        }
    }
}
