#nullable enable
using TinCan.Core.Domain.Look;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Turns a subject's camera the way a player looks at something: yaw and pitch from where the camera aims from (the
    /// rig's aim height above the body) to the middle of the object's colliders (its pivot when it has none). Interact
    /// targets what the player looks at, so a level look at an object's pivot on the deck is not what a player does.
    /// </summary>
    public static class ScenarioAim
    {
        public static (float Pitch, float Yaw) LookAt(ILookView look, Transform body, Transform target)
        {
            Vector3 from = body.position + body.up * look.AimHeight;
            Vector3 to = Middle(target) - from;
            if (to.sqrMagnitude < 0.0001f) return (0f, look.Yaw);

            Vector3 direction = to.normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg; // positive looks down
            pitch = Mathf.Clamp(pitch, -look.MaxPitch, look.MaxPitch);
            look.ApplyLook(pitch, yaw);
            return (pitch, yaw);
        }

        private static Vector3 Middle(Transform target)
        {
            var colliders = target.GetComponentsInChildren<Collider>();
            if (colliders.Length == 0) return target.position;

            var bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            return bounds.center;
        }
    }
}
