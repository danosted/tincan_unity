#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Ship;

namespace TinCan.Features.Helm
{
    /// <summary>A ship's helm: the station a player steers it from. It is an <see cref="Stations.IStation"/> too.</summary>
    public interface IHelm : IActor
    {
        /// <summary>The ship this helm steers (the one it is fixed to); null before it is parented to one.</summary>
        IAirshipView? Ship { get; }
    }
}
