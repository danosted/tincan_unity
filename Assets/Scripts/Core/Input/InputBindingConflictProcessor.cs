#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Domain, pure: may two actions share a key? Only when no two contexts that turn them on can be live at the same
    /// time: one blocks the other, or they follow different possessed kinds (walking and steering the ship are never
    /// live together, so Space can be Jump and pitch). Read straight from the context assets.
    /// </summary>
    public class InputBindingConflictProcessor
    {
        public bool CanShareAKey(InputActionId a, InputActionId b, IReadOnlyList<InputContext> contexts)
        {
            var withA = contexts.Where(context => context.Actions.Contains(a)).ToList();
            var withB = contexts.Where(context => context.Actions.Contains(b)).ToList();
            return !withA.Any(x => withB.Any(y => CanBeLiveTogether(x, y)));
        }

        public bool CanBeLiveTogether(InputContext a, InputContext b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (Blocks(a, b) || Blocks(b, a)) return false;
            bool bothFollowPossession = a.Activation == InputContextActivation.WhilePossessing && b.Activation == InputContextActivation.WhilePossessing;
            return !(bothFollowPossession && a.PossessedKind != b.PossessedKind);
        }

        private static bool Blocks(InputContext blocker, InputContext other) =>
            blocker.Blocks.Contains(other) || (blocker.BlocksAllLower && other.Priority < blocker.Priority);
    }
}
