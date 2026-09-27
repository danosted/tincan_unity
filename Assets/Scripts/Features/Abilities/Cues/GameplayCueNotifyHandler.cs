#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// Runs a <see cref="GameplayCueNotify"/>'s action lists as a cue handler. On removal it first stops what the OnActive
    /// actions started on that actor, then runs OnRemoved. One handler serves every actor: what an action holds is keyed
    /// by (action, actor) in the presenter.
    /// </summary>
    public sealed class GameplayCueNotifyHandler : IGameplayCueHandler
    {
        private readonly GameplayCueNotify _notify;
        private readonly IGameplayCuePresenter _presenter;

        public GameplayCueNotifyHandler(GameplayCueNotify notify, IGameplayCuePresenter presenter)
        {
            _notify = notify;
            _presenter = presenter;
        }

        public GameplayTag? Cue => _notify.Cue;

        public void OnExecute(in GameplayCueEvent cueEvent) => RunAll(_notify.OnExecute, cueEvent, GameplayCueEventKind.Execute);

        public void OnActive(in GameplayCueEvent cueEvent) => RunAll(_notify.OnActive, cueEvent, GameplayCueEventKind.Active);

        public void OnRemoved(in GameplayCueEvent cueEvent)
        {
            foreach (var action in _notify.OnActive)
            {
                if (action != null) action.Stop(cueEvent, Context(action, cueEvent, GameplayCueEventKind.Removed));
            }
            RunAll(_notify.OnRemoved, cueEvent, GameplayCueEventKind.Removed);
        }

        private void RunAll(IReadOnlyList<GameplayCueAction?> actions, in GameplayCueEvent cueEvent, GameplayCueEventKind phase)
        {
            foreach (var action in actions)
            {
                if (action != null) action.Run(cueEvent, Context(action, cueEvent, phase));
            }
        }

        private GameplayCueActionContext Context(GameplayCueAction action, in GameplayCueEvent cueEvent, GameplayCueEventKind phase) =>
            new(_presenter, phase, new GameplayCueInstanceKey(action, cueEvent.Controller.Id));
    }
}
