#nullable enable
using TinCan.Core.Humanoid;
using UnityEngine;

namespace TinCan.Features.Stations
{
    /// <summary>
    /// Infrastructure: the seat check against the physics scene, on the server. A seat is the body's root (the capsule
    /// centre), so the floor must lie within the root-to-feet distance plus <see cref="FloorBelowFeet"/> under it, and
    /// the body's capsule, lifted by a step, must overlap nothing solid. Triggers, the player's own colliders and the
    /// station's own colliders are ignored.
    /// </summary>
    public sealed class PhysicsStationSeatCheck : IStationSeatCheck
    {
        private const float FloorBelowFeet = 0.5f;
        private const float StepHeight = 0.3f;
        private const float Skin = 0.05f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly Collider[] _overlaps = new Collider[16];

        public bool CanSeat(IHumanoidCharacterView player, IStation station, Transform seat)
        {
            var body = player.Movement.Transform;
            var stationRoot = station is Component component && component != null ? component.transform : null;
            Vector3 up = seat.up;

            var controller = body.GetComponent<CharacterController>();
            float scale = body.lossyScale.y;
            float height = controller != null ? controller.height * scale : 2f;
            float radius = controller != null ? controller.radius * scale : 0.4f;
            float rootAboveFeet = controller != null ? (controller.height * 0.5f - controller.center.y) * scale + controller.skinWidth : 1f;

            int count = Physics.RaycastNonAlloc(seat.position, -up, _hits, rootAboveFeet + FloorBelowFeet, ~0, QueryTriggerInteraction.Ignore);
            bool floor = false;
            for (int i = 0; i < count && !floor; i++) floor = !IsOwn(_hits[i].collider, body, null);
            if (!floor) return false;

            Vector3 feet = seat.position - up * rootAboveFeet;
            Vector3 bottom = feet + up * (StepHeight + radius);
            Vector3 top = feet + up * Mathf.Max(StepHeight + radius, height - radius);
            count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius - Skin, _overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsOwn(_overlaps[i], body, stationRoot)) return false;
            }
            return true;
        }

        private static bool IsOwn(Collider collider, Transform body, Transform? station) =>
            collider.transform.IsChildOf(body) || (station != null && collider.transform.IsChildOf(station));
    }
}
