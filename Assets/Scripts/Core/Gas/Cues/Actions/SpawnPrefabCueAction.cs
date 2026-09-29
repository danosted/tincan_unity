#nullable enable
using UnityEngine.Scripting.APIUpdating;
using System;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Core.Gas.Cues.Actions
{
    /// <summary>
    /// A pooled prefab (particles, a looping AudioSource, a light) parented to the target, so it stays ship-local. In
    /// OnExecute and OnRemoved it lives for <see cref="Lifetime"/> seconds; in OnActive it lives until the cue is removed.
    /// </summary>
    [Serializable]
    // Stored by [SerializeReference] in GCN_* assets with its namespace and assembly; this keeps old assets loading.
    [MovedFrom(false, sourceNamespace: "TinCan.Features.Abilities.Cues.Actions", sourceAssembly: "TinCan.Features")]
    public sealed class SpawnPrefabCueAction : GameplayCueAction
    {
        public GameObject? Prefab;
        [Tooltip("Offset from the target, in the target's local space.")]
        public Vector3 LocalOffset;
        [Tooltip("Seconds before a burst's instance returns to the pool. Ignored in OnActive.")]
        [Min(0.05f)] public float Lifetime = 2f;

        public override void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context)
        {
            if (Prefab == null || cueEvent.Target == null) return;

            if (context.Phase == GameplayCueEventKind.Active) context.Presenter.SpawnHeld(context.Key, Prefab, cueEvent.Target, LocalOffset);
            else context.Presenter.SpawnTimed(Prefab, cueEvent.Target, LocalOffset, Lifetime);
        }

        public override void Stop(in GameplayCueEvent cueEvent, in GameplayCueActionContext context) => context.Presenter.Release(context.Key);
    }
}
