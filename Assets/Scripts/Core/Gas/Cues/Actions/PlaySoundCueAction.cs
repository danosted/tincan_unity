#nullable enable
using UnityEngine.Scripting.APIUpdating;
using System;
using TinCan.Core.Domain.Audio;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Core.Gas.Cues.Actions
{
    /// <summary>A one-shot clip at the target's position. For a loop that follows the target, use a
    /// <see cref="SpawnPrefabCueAction"/> with a looping AudioSource in OnActive.</summary>
    [Serializable]
    // Stored by [SerializeReference] in GCN_* assets with its namespace and assembly; this keeps old assets loading.
    [MovedFrom(false, sourceNamespace: "TinCan.Features.Abilities.Cues.Actions", sourceAssembly: "TinCan.Features")]
    public sealed class PlaySoundCueAction : GameplayCueAction
    {
        public AudioClip? Clip;
        [Range(0f, 1f)] public float Volume = 1f;

        [Tooltip("The mixer group it plays through. World and Ambience play in 3D at the target; the others in 2D.")]
        public AudioChannel Channel = AudioChannel.World;

        public override void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context)
        {
            if (Clip == null || cueEvent.Target == null) return;
            context.Presenter.PlaySound(Clip, Channel, IsSpatial(Channel) ? cueEvent.Target.position : null, Volume);
        }

        private static bool IsSpatial(AudioChannel channel) => channel is AudioChannel.World or AudioChannel.Ambience;
    }
}
