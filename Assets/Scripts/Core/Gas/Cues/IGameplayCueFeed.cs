#nullable enable
using System;
using TinCan.Core.Domain.Cues;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// Every cue this peer handled, as it happens. For observers outside the game (the scenario harness counts cues
    /// through it), so no test concern lives in the cue code itself.
    /// </summary>
    public interface IGameplayCueFeed
    {
        event Action<GameplayCueEventKind, GameplayCueEvent>? CueHandled;
    }
}
