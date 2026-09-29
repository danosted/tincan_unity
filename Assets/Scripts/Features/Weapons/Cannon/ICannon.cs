#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>What every peer learns about one shot: sent once when it is fired, and once when it ends.</summary>
    public readonly struct CannonShotEvent
    {
        public readonly int ShotId;
        public readonly bool Ended;
        public readonly Vector3 Origin;
        public readonly Vector3 Velocity;
        public readonly Vector3 Point;

        private CannonShotEvent(int shotId, bool ended, Vector3 origin, Vector3 velocity, Vector3 point)
        {
            ShotId = shotId;
            Ended = ended;
            Origin = origin;
            Velocity = velocity;
            Point = point;
        }

        public static CannonShotEvent Fired(int shotId, Vector3 origin, Vector3 velocity) => new(shotId, false, origin, velocity, origin);
        public static CannonShotEvent Finished(int shotId, Vector3 point) => new(shotId, true, default, default, point);
    }

    /// <summary>
    /// A ship cannon: the barrel pivots, the muzzle, the replicated aim (for peers that do not occupy it) and the shot
    /// events every peer draws from. The simulation lives in <see cref="CannonFireUseCase"/>; this is only the thing.
    /// </summary>
    public interface ICannon : IActor
    {
        Transform? Base { get; }
        Transform? YawPivot { get; }
        Transform? PitchPivot { get; }
        Transform? Muzzle { get; }

        /// <summary>The ship this cannon is mounted on, whose colliders its shots pass through.</summary>
        Transform? ShipRoot { get; }

        /// <summary>Barrel angles in degrees, base frame (elevation positive up); written by the server.</summary>
        float Yaw { get; }
        float Elevation { get; }

        void ServerSetAim(float yaw, float elevation);
        void ServerShotFired(int shotId, Vector3 origin, Vector3 velocity);
        void ServerShotEnded(int shotId, Vector3 point);

        /// <summary>Every peer: the next shot event received, oldest first.</summary>
        bool TryTakeShotEvent(out CannonShotEvent shotEvent);

        /// <summary>Presentation: turns the barrel.</summary>
        void ApplyAim(float yaw, float elevation);

        /// <summary>Presentation: the aiming arc for the local occupant (null hides it).</summary>
        void ShowAimPreview(Vector3[]? points, int count);
    }
}
