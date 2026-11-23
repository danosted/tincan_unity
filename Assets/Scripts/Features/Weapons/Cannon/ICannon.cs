#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// What every peer learns about one shot: sent once when the charge starts, and once when the beam fires, with
    /// where it went (from the muzzle to what it hit, or to its range).
    /// </summary>
    public readonly struct CannonShotEvent
    {
        public readonly int ShotId;
        public readonly bool HasFired;
        public readonly Vector3 Origin;
        public readonly Vector3 End;

        private CannonShotEvent(int shotId, bool hasFired, Vector3 origin, Vector3 end)
        {
            ShotId = shotId;
            HasFired = hasFired;
            Origin = origin;
            End = end;
        }

        public static CannonShotEvent Charging(int shotId) => new(shotId, false, default, default);
        public static CannonShotEvent Fired(int shotId, Vector3 origin, Vector3 end) => new(shotId, true, origin, end);
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

        /// <summary>The ship this cannon is mounted on. A shot that hits its colliders is a misfire and damages it.</summary>
        Transform? ShipRoot { get; }

        /// <summary>The ability controller of the ship this cannon is mounted on (its health), or null when unmounted.</summary>
        IAbilityControllerBase? ShipController { get; }

        /// <summary>Barrel angles in degrees, base frame (elevation positive up); written by the server.</summary>
        float Yaw { get; }
        float Elevation { get; }

        void ServerSetAim(float yaw, float elevation);
        void ServerShotCharging(int shotId);
        void ServerShotFired(int shotId, Vector3 origin, Vector3 end);

        /// <summary>Every peer: the next shot event received, oldest first.</summary>
        bool TryTakeShotEvent(out CannonShotEvent shotEvent);

        /// <summary>Presentation: turns the barrel.</summary>
        void ApplyAim(float yaw, float elevation);

        /// <summary>Presentation: the aiming line for the local occupant (null hides it).</summary>
        void ShowAimPreview(Vector3[]? points, int count);
    }
}
