#nullable enable
using UnityEngine.Scripting.APIUpdating;
using System;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Core.Gas.Cues.Actions
{
    /// <summary>A HUD line for a few seconds. The same text key restarts its time instead of stacking.</summary>
    [Serializable]
    // Stored by [SerializeReference] in GCN_* assets with its namespace and assembly; this keeps old assets loading.
    [MovedFrom(false, sourceNamespace: "TinCan.Features.Abilities.Cues.Actions", sourceAssembly: "TinCan.Features")]
    public sealed class HudToastCueAction : GameplayCueAction
    {
        public string Text = string.Empty;
        [Min(0.1f)] public float Seconds = 3f;

        public override void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context)
        {
            if (string.IsNullOrWhiteSpace(Text)) return;
            context.Presenter.ShowHudText(Text, string.Empty, Seconds);
        }
    }
}
