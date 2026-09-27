#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Features.Abilities.Cues.Handlers
{
    /// <summary>
    /// Sets an Animator bool while a state cue is active (a tool's working animation, say). The Animator is found on
    /// this GameObject or below it; a burst sets a trigger of the same name instead.
    /// </summary>
    public sealed class AnimatorBoolCueHandler : MonoBehaviour, IGameplayCueHandler
    {
        [SerializeField] private GameplayTag? _cue;
        [SerializeField] private string _parameter = string.Empty;

        private Animator? _animator;

        public GameplayTag? Cue => _cue;

        public void OnExecute(in GameplayCueEvent cueEvent)
        {
            if (TryGetAnimator(out var animator)) animator.SetTrigger(_parameter);
        }

        public void OnActive(in GameplayCueEvent cueEvent)
        {
            if (TryGetAnimator(out var animator)) animator.SetBool(_parameter, true);
        }

        public void OnRemoved(in GameplayCueEvent cueEvent)
        {
            if (TryGetAnimator(out var animator)) animator.SetBool(_parameter, false);
        }

        private bool TryGetAnimator(out Animator animator)
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>(includeInactive: true);
            animator = _animator!;
            return _animator != null && !string.IsNullOrEmpty(_parameter);
        }
    }
}
