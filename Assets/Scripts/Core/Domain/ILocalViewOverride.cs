#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// A camera the local player looks through instead of their possession's own, while it is non-null (the camera of a
    /// station they occupy). <see cref="ILocalViewCamera"/> asks it first.
    /// </summary>
    public interface ILocalViewOverride
    {
        Camera? Camera { get; }
    }
}
