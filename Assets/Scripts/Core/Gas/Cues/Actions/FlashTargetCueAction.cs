#nullable enable
using System;
using TinCan.Core.Domain.Cues;
using UnityEngine;

namespace TinCan.Core.Gas.Cues.Actions
{
    /// <summary>
    /// Blinks the target actor's own renderers toward <see cref="Color"/> (not those of a nested actor, such as a player
    /// aboard a ship) and gives them their colour back. For a burst (GCN_Hit: whatever takes a hit blinks red).
    /// </summary>
    [Serializable]
    public sealed class FlashTargetCueAction : GameplayCueAction
    {
        [Tooltip("The colour the target blinks toward; alpha is how far (1: fully this colour).")]
        public Color Color = new(1f, 0.1f, 0.05f, 0.6f);

        [Tooltip("Seconds the flash lasts (two blinks).")]
        [Min(0.05f)] public float Seconds = 0.4f;

        public override void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context)
        {
            if (cueEvent.Target == null) return;
            context.Presenter.Flash(cueEvent.Target, cueEvent.Controller.Id, Color, Seconds);
        }
    }
}
