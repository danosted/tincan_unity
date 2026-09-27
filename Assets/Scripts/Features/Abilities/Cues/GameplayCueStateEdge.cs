#nullable enable
namespace TinCan.Features.Abilities.Cues
{
    /// <summary>What changed for one state cue on one actor since the last observation.</summary>
    public enum GameplayCueStateEdge
    {
        None,
        Active,
        Removed
    }
}
