#nullable enable
using System;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>Identifies what one action started on one actor, so the matching removal can end exactly that.</summary>
    public readonly struct GameplayCueInstanceKey : IEquatable<GameplayCueInstanceKey>
    {
        public readonly object Source;
        public readonly Guid ActorId;

        public GameplayCueInstanceKey(object source, Guid actorId)
        {
            Source = source;
            ActorId = actorId;
        }

        public bool Equals(GameplayCueInstanceKey other) => ReferenceEquals(Source, other.Source) && ActorId == other.ActorId;
        public override bool Equals(object? obj) => obj is GameplayCueInstanceKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Source), ActorId);
    }
}
