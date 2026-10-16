#nullable enable
namespace TinCan.Features.Respawn
{
    /// <summary>What happens to the item a player holds when they respawn.</summary>
    public enum CarriedItemOutcome
    {
        /// <summary>They respawn still holding it.</summary>
        Keep,

        /// <summary>It is gone: they respawn empty-handed. Racks are unlimited, so a tool can be taken again.</summary>
        Lose
    }
}
