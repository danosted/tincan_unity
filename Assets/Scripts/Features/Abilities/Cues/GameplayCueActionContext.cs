#nullable enable
namespace TinCan.Features.Abilities.Cues
{
    /// <summary>What an action runs with: the presenter, and which list it runs from.</summary>
    public readonly struct GameplayCueActionContext
    {
        public readonly IGameplayCuePresenter Presenter;
        /// <summary>Execute (a burst), Active (lives until the cue is removed) or Removed.</summary>
        public readonly GameplayCueEventKind Phase;
        /// <summary>The key for what this action holds on this actor while the cue is active.</summary>
        public readonly GameplayCueInstanceKey Key;

        public GameplayCueActionContext(IGameplayCuePresenter presenter, GameplayCueEventKind phase, GameplayCueInstanceKey key)
        {
            Presenter = presenter;
            Phase = phase;
            Key = key;
        }
    }
}
