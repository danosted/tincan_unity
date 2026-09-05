#nullable enable
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Automation seam for playtests: lets scripts (Editor CLI, future PlayMode tests) press actions by name.
    /// UnityInputService merges this with the physical devices, so gameplay code needs no changes.
    /// </summary>
    public interface IScriptedInput
    {
        /// <summary>Holds an action down until <see cref="Release"/>.</summary>
        void Press(string actionName);
        void Release(string actionName);
        /// <summary>Registers a one-shot trigger; it stays visible until the end of the first frame in which it is read.</summary>
        void Tap(string actionName);
        void Clear();
    }

    public sealed class ScriptedInput : IScriptedInput, ILateTickable
    {
        private readonly HashSet<string> _held = new();
        private readonly Dictionary<string, int> _tapSeenFrame = new();
        private readonly List<string> _consumed = new();

        public void Press(string actionName) => _held.Add(actionName);
        public void Release(string actionName) => _held.Remove(actionName);
        public void Tap(string actionName) => _tapSeenFrame[actionName] = -1;

        public void Clear()
        {
            _held.Clear();
            _tapSeenFrame.Clear();
        }

        public bool IsPressed(string actionName) => _held.Contains(actionName);

        public bool WasTriggered(string actionName)
        {
            if (!_tapSeenFrame.ContainsKey(actionName)) return false;
            _tapSeenFrame[actionName] = Time.frameCount;
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
