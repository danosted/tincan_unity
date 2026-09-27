#nullable enable
using System;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Features.Abilities.Cues.Actions
{
    /// <summary>A one-shot clip at the target's position. For a loop that follows the target, use a
    /// <see cref="SpawnPrefabCueAction"/> with a looping AudioSource in OnActive.</summary>
    [Serializable]
    public sealed class PlaySoundCueAction : GameplayCueAction
    {
        public AudioClip? Clip;
        [Range(0f, 1f)] public float Volume = 1f;

        public override void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context)
        {
            if (Clip == null || cueEvent.Target == null) return;
            context.Presenter.PlaySound(Clip, cueEvent.Target.position, Volume);
        }
    }
}
