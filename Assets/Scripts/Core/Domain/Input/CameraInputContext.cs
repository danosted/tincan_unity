#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// Mouse look for whatever the local player controls (body, airship, free camera). Always active; the menu and
    /// stations that aim with the mouse block it.
    /// </summary>
    [CreateAssetMenu(fileName = "Context_Camera", menuName = "TinCan/Input/Camera Context")]
    public sealed class CameraInputContext : InputContext
    {
        [Tooltip("Look delta (Vector2, mouse counts per frame).")]
        public InputActionId? Look;

        protected override IEnumerable<InputActionId?> Slots => new[] { Look };
    }
}
