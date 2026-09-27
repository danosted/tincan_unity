#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// Turns "is this cue tag on this actor now?" into edges: <see cref="GameplayCueStateEdge.Active"/> when it appears
    /// (including the first observation of an actor that joined with it), <see cref="GameplayCueStateEdge.Removed"/> when
    /// it goes. <see cref="Forget"/> drops an actor without edges, so a despawn never plays a "removed" moment.
    /// </summary>
    public sealed class GameplayCueStateTracker
    {
        private readonly Dictionary<Guid, HashSet<GameplayTag>> _active = new();

        public GameplayCueStateEdge Observe(Guid actorId, GameplayTag cue, bool present)
        {
            bool known = _active.TryGetValue(actorId, out var cues);
            bool wasPresent = known && cues!.Contains(cue);

            switch (present, wasPresent)
            {
                case (true, false):
                    if (!known) _active[actorId] = cues = new HashSet<GameplayTag>();
                    cues!.Add(cue);
                    return GameplayCueStateEdge.Active;
                case (false, true):
                    cues!.Remove(cue);
                    if (cues.Count == 0) _active.Remove(actorId);
                    return GameplayCueStateEdge.Removed;
                default:
                    return GameplayCueStateEdge.None;
            }
        }

        public bool IsActive(Guid actorId, GameplayTag cue) =>
            _active.TryGetValue(actorId, out var cues) && cues.Contains(cue);

        /// <summary>Teardown: forget the actor's active cues without reporting them as removed.</summary>
        public void Forget(Guid actorId) => _active.Remove(actorId);
    }
}
