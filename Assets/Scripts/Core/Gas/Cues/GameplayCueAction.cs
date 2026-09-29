#nullable enable
using System;
using TinCan.Core.Domain.Cues;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// One small, reusable piece of what a cue does, listed in a <see cref="GameplayCueNotify"/> (picked by type in the
    /// Inspector). A new kind of presentation is a new subclass; nothing existing changes. One instance serves every
    /// actor, so an action keeps no per-actor state: what it holds lives in the presenter under the context's key.
    /// </summary>
    [Serializable]
    public abstract class GameplayCueAction
    {
        public abstract void Run(in GameplayCueEvent cueEvent, in GameplayCueActionContext context);

        /// <summary>For actions in the OnActive list: the cue was removed, so end what <see cref="Run"/> started.</summary>
        public virtual void Stop(in GameplayCueEvent cueEvent, in GameplayCueActionContext context) { }
    }
}
