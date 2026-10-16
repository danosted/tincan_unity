#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using TinCan.Core.Humanoid;
using TinCan.Core.UI;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// This peer, per frame, the local player's choices.
    /// - **Overboard** (the replicated overboard tag): the choice is open: respawn, or hang on the leash where the ship has a
    ///   leash point. It reopens if it is closed before choosing, and again if a leash choice gets no answer (the server
    ///   refused it), so nobody is stranded. The HUD line "Respawn" counts down the rule's delay from when this peer saw the
    ///   fall; the server keeps the real clock.
    /// - **Leashed** (the leashed tag): the HUD says how to climb and how to respawn (Interact opens the choice,
    ///   <see cref="OpenLeashMenuInputHandler"/>); nothing is forced, so the player can climb.
    /// </summary>
    public sealed class OverboardMenuUseCase : ITickable
    {
        public const string MenuId = OverboardMenus.OverboardMenuId;
        public const string HudKey = "Respawn";

        // A leash choice the server has not answered in this long is taken as refused: the choice opens again.
        private const float LeashAnswerSeconds = 3f;

        private readonly RespawnRules _rules;
        private readonly IActorRegistry _actors;
        private readonly IMenuSystem _menus;
        private readonly IHudValues _hud;
        private readonly ITimeService _time;
        private readonly OverboardChoice _choice;
        private bool _wasOverboard;
        private bool _shown;
        private float _since;

        public OverboardMenuUseCase(RespawnRules rules, IActorRegistry actors, IMenuSystem menus, IHudValues hud, ITimeService time, OverboardChoice choice)
        {
            _rules = rules;
            _actors = actors;
            _menus = menus;
            _hud = hud;
            _time = time;
            _choice = choice;
        }

        public void Tick()
        {
            var local = _actors.GetLocalPlayerActor<IHumanoidCharacterView>();
            bool overboard = Has(local, _rules.OverboardTag);
            bool leashed = Has(local, _rules.LeashedTag);

            if (overboard && !_wasOverboard)
            {
                _since = _time.Time;
                _choice.Clear();
            }

            _wasOverboard = overboard;

            if (!overboard && !leashed)
            {
                if (!_shown) return;
                _hud.Remove(HudKey);
                if (_menus.Current?.MenuId is OverboardMenus.OverboardMenuId or OverboardMenus.LeashMenuId) _menus.CloseAll();
                _shown = false;
                return;
            }

            _shown = true;
            if (leashed)
            {
                _hud.Set(HudKey, LeashedHudText(_rules.CanClimb));
                return;
            }

            var rule = RespawnProcessor.RuleFor(_rules.Rules, RespawnCause.Overboard);
            if (_choice.Kind == OverboardChoiceKind.Leash && _time.Time - _choice.ChosenAt > LeashAnswerSeconds) _choice.Clear();

            float remaining = RespawnProcessor.Remaining(_since, _time.Time, rule.DelaySeconds);
            _hud.Set(HudKey, HudText(_choice.Chosen, remaining));

            if (!_choice.Chosen && _menus.Current?.MenuId != MenuId) _menus.Open(OverboardMenus.Overboard(rule, CanLeash()));
        }

        /// <summary>The HUD line while overboard: the countdown, or what happens next.</summary>
        public static string HudText(bool chosen, float remaining) => (chosen, remaining > 0f) switch
        {
            (_, true) => $"Overboard: respawn in {Mathf.CeilToInt(remaining)} s",
            (true, false) => "Respawning",
            _ => "Overboard"
        };

        /// <summary>The HUD line while hanging on the leash.</summary>
        public static string LeashedHudText(bool canClimb) => canClimb
            ? "On the leash: hold forward to climb, Interact to respawn"
            : "On the leash: wait for the winch, Interact to respawn";

        private bool CanLeash() => _rules.LeashedEffect != null && _actors.GetActors<ILeashPoint>().Any(point => point.IsSimulating);

        private static bool Has(IHumanoidCharacterView? player, TinCan.Core.Domain.Abilities.Tags.GameplayTag? tag) =>
            player != null && tag != null && player.HasTag(tag);
    }
}
