#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Hud;
using VContainer.Unity;

namespace TinCan.Features.SkillChecks.Challenges
{
    /// <summary>
    /// Presentation, every peer, every frame: while a challenge lights stations, one HUD line names it and lists them
    /// with the seconds left to start each ("Head wind: Boiler 12 s, Engine telegraph (on it)"). It reads the hosts'
    /// replicated lit state, so every crew member sees where to go. Plan: hazards-and-boons.md (B4a).
    /// </summary>
    public sealed class SkillChallengeHudPresenter : ITickable
    {
        public const string HudKey = "Challenge";

        private readonly IActorRegistry _actors;
        private readonly IHudValues _hud;
        private readonly List<string> _parts = new();

        public SkillChallengeHudPresenter(IActorRegistry actors, IHudValues hud)
        {
            _actors = actors;
            _hud = hud;
        }

        public void Tick()
        {
            string? title = null;
            _parts.Clear();
            foreach (var host in _actors.GetActors<ISkillCheckHost>())
            {
                var lit = host.LitChallenge;
                if (lit == null || host.Definition == null) continue;

                title ??= lit;
                int seconds = host.LitSecondsLeft;
                _parts.Add(seconds < 0 ? $"{host.Definition.Station} (on it)" : $"{host.Definition.Station} {seconds} s");
            }

            if (title == null) _hud.Remove(HudKey);
            else _hud.Set(HudKey, $"{title}: {string.Join(", ", _parts)}");
        }
    }
}
