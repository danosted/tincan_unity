#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Every peer: the layout of the current play session (a voyage), replicated by the session's state actor. Features
    /// that generate the world per session (sky islands) build from <see cref="LayoutSeed"/> and keep the session's start
    /// and destination clear. Absent without a session; a <see cref="LayoutSeed"/> of 0 means no session has begun yet.
    /// </summary>
    public interface ISessionLayout : IActor
    {
        /// <summary>The seed for anything generated per session; 0 until the first session begins.</summary>
        int LayoutSeed { get; }

        /// <summary>Where the session started (the ship's position when it began).</summary>
        Vector3 Origin { get; }

        /// <summary>Where the session is headed.</summary>
        Vector3 Destination { get; }
    }
}
