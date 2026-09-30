#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Domain, pure: which contexts are live and which actions they turn on. Contexts whose condition holds are walked
    /// from the highest priority down; a context blocked by a live one above it is skipped (and blocks nothing itself).
    /// </summary>
    public class InputContextProcessor
    {
        private readonly HashSet<InputContext> _blocked = new();

        /// <summary>Fills <paramref name="live"/> (highest priority first) and <paramref name="enabled"/>.</summary>
        public void Resolve(IEnumerable<InputContext> contexts, Func<InputContext, bool> isActive,
            List<InputContext> live, HashSet<InputActionId> enabled)
        {
            live.Clear();
            enabled.Clear();
            _blocked.Clear();
            int? blockBelow = null;

            var ordered = contexts
                .Where(context => context != null && isActive(context))
                .OrderByDescending(context => context.Priority)
                .ThenBy(context => context.name, StringComparer.Ordinal);

            foreach (var context in ordered)
            {
                if (_blocked.Contains(context) || context.Priority < blockBelow) continue;

                live.Add(context);
                foreach (var blocked in context.Blocks) if (blocked != null) _blocked.Add(blocked);
                if (context.BlocksAllLower) blockBelow = Math.Max(blockBelow ?? int.MinValue, context.Priority);
                foreach (var action in context.Actions) enabled.Add(action);
            }
        }
    }
}
