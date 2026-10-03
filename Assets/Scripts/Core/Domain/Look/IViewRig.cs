#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Look
{
    /// <summary>
    /// Where the local player's camera sits: third person, first person, ... A camera feature registers one
    /// (<c>.As&lt;IViewRig&gt;()</c>) and the profile picks the feature; the first registered rig is used. Every peer
    /// loads the same profile, so <see cref="AimHeight"/>, which targeting uses on the server too, agrees everywhere.
    /// </summary>
    public interface IViewRig
    {
        /// <summary>The local player starts looking through <paramref name="camera"/>; <paramref name="body"/> is their own body's renderers.</summary>
        void Take(Camera camera, IReadOnlyList<Renderer> body);

        /// <summary>The local player stops looking through it (possessed something else): undo what <see cref="Take"/> changed.</summary>
        void Release(Camera camera, IReadOnlyList<Renderer> body);

        /// <summary>The camera pose for this frame.</summary>
        (Vector3 Position, Quaternion Rotation) Place(in ViewPose pose);

        /// <summary>Height above the body root the camera looks from, given the body's eye height (targeting CameraAim).</summary>
        float AimHeight(float eyeHeight);
    }
}
