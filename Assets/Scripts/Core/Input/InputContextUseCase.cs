#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Input;
using VContainer.Unity;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Application Layer, every peer, every frame: evaluates every context's condition, resolves blocking
    /// (<see cref="InputContextProcessor"/>) and switches the actions of the live contexts on and all others off. It is the
    /// only writer of which contexts are live; nothing pushes or pops them, so no context outlives its condition.
    /// </summary>
    public sealed class InputContextUseCase : IInputContexts, IInitializable, ITickable
    {
        private readonly InputContextSet _contexts;
        private readonly IInputContextConditions _conditions;
        private readonly IInputActionSwitch _actions;
        private readonly InputContextProcessor _processor;
        private readonly Func<InputContext, bool> _holds;
        private List<InputContext> _live = new();
        private List<InputContext> _scratch = new();
        private readonly HashSet<InputActionId> _enabled = new();
        private readonly HashSet<InputActionId> _lastEnabled = new();
        private bool _evaluated;

        public InputContextUseCase(InputContextSet contexts, IInputContextConditions conditions, IInputActionSwitch actions, InputContextProcessor processor)
        {
            _contexts = contexts;
            _conditions = conditions;
            _actions = actions;
            _processor = processor;
            _holds = _conditions.Holds;
        }

        public IReadOnlyList<InputContext> Active => _live;

        /// <summary>Every context this scene evaluates, live or not (for the Input Map report).</summary>
        public IReadOnlyList<InputContext> All => _contexts.All;

        public event Action? Changed;

        public bool IsActive(InputContext? context) => context != null && _live.Contains(context);

        public void Initialize() => Evaluate();

        public void Tick() => Evaluate();

        public void Evaluate()
        {
            _processor.Resolve(_contexts.All, _holds, _scratch, _enabled);

            if (!_evaluated || !_enabled.SetEquals(_lastEnabled))
            {
                _lastEnabled.Clear();
                _lastEnabled.UnionWith(_enabled);
                _actions.SetEnabled(_lastEnabled);
            }

            bool changed = !_evaluated || !SameOrder(_scratch, _live);
            (_live, _scratch) = (_scratch, _live);
            _evaluated = true;
            if (changed) Changed?.Invoke();
        }

        private static bool SameOrder(List<InputContext> a, List<InputContext> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }
    }
}
