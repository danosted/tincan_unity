#nullable enable
using TinCan.Core.Domain.Events;
using TinCan.Core.Domain.Input;

namespace TinCan.DevTools
{
    /// <summary>Presses <see cref="ScriptedAction"/>s through <see cref="IScriptedInput"/>, for bots and scenarios.</summary>
    public sealed class ScriptedActionDriver
    {
        private const string LogSource = "NetHarness";

        private readonly IScriptedInput _input;
        private readonly ScriptedActionMap _map;
        private readonly IEventPublisher _events;

        public ScriptedActionDriver(IScriptedInput input, ScriptedActionMap map, IEventPublisher events)
        {
            _input = input;
            _map = map;
            _events = events;
        }

        public void Press(ScriptedAction action)
        {
            if (Resolve(action, out var id, out var value)) _input.Press(id, value);
        }

        public void Release(ScriptedAction action)
        {
            if (Resolve(action, out var id, out var value)) _input.Release(id, value);
        }

        public void Tap(ScriptedAction action)
        {
            if (Resolve(action, out var id, out _)) _input.Tap(id);
        }

        private bool Resolve(ScriptedAction action, out InputActionId id, out UnityEngine.Vector2 value)
        {
            if (_map.TryResolve(action, out id, out value)) return true;
            _events.LogWarning(LogSource, $"Scripted {action} has no action: its input context is not loaded.");
            return false;
        }
    }
}
