#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Features.Abilities.Cues.Handlers
{
    /// <summary>
    /// Shows this GameObject while a state cue is active on the actor it belongs to (the nearest ability controller
    /// above it), and hides it when the cue is removed. For things that are part of the model, such as a damage marker.
    /// Author the object hidden; a late joiner still sees it, because cues already active arrive as Active.
    /// </summary>
    public sealed class ToggleObjectCueHandler : MonoBehaviour, IGameplayCueHandler
    {
        [SerializeField] private GameplayTag? _cue;

        public GameplayTag? Cue => _cue;

        public void OnExecute(in GameplayCueEvent cueEvent) { }

        public void OnActive(in GameplayCueEvent cueEvent) => gameObject.SetActive(true);

        public void OnRemoved(in GameplayCueEvent cueEvent) => gameObject.SetActive(false);
    }
}
