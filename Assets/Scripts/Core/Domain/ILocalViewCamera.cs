#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// The camera the local player looks through right now (the possessed actor's camera), or null before the player
    /// has spawned. Scenes have no camera of their own, so ask this instead of <c>Camera.main</c>.
    /// </summary>
    public interface ILocalViewCamera
    {
        Camera? Camera { get; }
    }
}
