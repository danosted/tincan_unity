#nullable enable
namespace TinCan.Features.Respawn
{
    /// <summary>Why a player is respawning. Each cause has its own rule in <see cref="RespawnRules"/>.</summary>
    public enum RespawnCause
    {
        /// <summary>Fell overboard: sank too far below the clouds.</summary>
        Overboard
    }
}
