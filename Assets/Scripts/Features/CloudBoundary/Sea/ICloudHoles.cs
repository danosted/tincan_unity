#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The holes in the cloud sea (plan .docs/plans/cloud-sea.md, "The drawn sea and its holes"). The server opens them;
    /// every peer reads their shapes now, the same on every peer at the same shared time. Whatever asks for a hole (the
    /// weather, a place, an event) picks its size; the sea works at any size.
    /// </summary>
    public interface ICloudHoles
    {
        /// <summary>The most holes open at once (the sea shader's limit).</summary>
        const int MaxHoles = 16;

        /// <summary>The holes as opened, shut ones included until the server drops them.</summary>
        IReadOnlyList<CloudHole> Holes { get; }

        /// <summary>Every hole's shape at the shared time now.</summary>
        IReadOnlyList<CloudHoleShape> ShapesNow { get; }

        /// <summary>Server only: opens <paramref name="hole"/>. False on a client, without the hole state, or when
        /// <see cref="MaxHoles"/> are open.</summary>
        bool TryOpen(CloudHole hole);
    }
}
