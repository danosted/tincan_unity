#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Input;
using TinCan.Core.Possession;
using TinCan.Core.UI;
using UnityEngine;

namespace TinCan.Core.Input
{
    /// <summary>
    /// The facts contexts activate on, read from the systems that own them: what the local player possesses (and its
    /// tags, replicated from the server), whether a menu is open, whether the Controls menu waits for a key, and which
    /// contexts their owners opened.
    /// </summary>
    public sealed class InputContextConditions : IInputContextConditions
    {
        private readonly IPossessionState _possession;
        private readonly IMenuSystem _menus;
        private readonly InputRebindState _rebinding;
        private readonly IInputContextSwitch _switch;

        public InputContextConditions(IPossessionState possession, IMenuSystem menus, InputRebindState rebinding,
            IInputContextSwitch? contextSwitch = null)
        {
            _possession = possession;
            _menus = menus;
            _rebinding = rebinding;
            _switch = contextSwitch ?? new InputContextSwitch();
        }

        public bool Holds(InputContext context) => context.Activation switch
        {
            InputContextActivation.Always => true,
            InputContextActivation.WhilePossessing => Possessed() is { } possessed && IsKind(possessed, context.PossessedKind),
            InputContextActivation.WhilePossessedHasTag => context.Tag != null && Possessed() is IAbilityControllerBase controller && controller.HasTag(context.Tag),
            InputContextActivation.WhileMenuOpen => _menus.IsOpen,
            InputContextActivation.WhileRebinding => _rebinding.IsRebinding,
            InputContextActivation.WhileOpened => _switch.IsOpen(context),
            _ => false,
        };

        private IPossessable? Possessed()
        {
            var possessed = _possession.CurrentPossession;
            return possessed is Component component && component == null ? null : possessed;
        }

        private static bool IsKind(IPossessable possessed, PossessedActorKind kind) => kind switch
        {
            PossessedActorKind.Humanoid => possessed is IHumanoidActor,
            _ => possessed is not IHumanoidActor,
        };
    }
}
