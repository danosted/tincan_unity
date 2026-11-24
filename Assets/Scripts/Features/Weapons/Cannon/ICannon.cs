#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using UnityEngine;

namespace TinCan.Features.Weapons.Cannon
{
    /// <summary>
    /// One pulse of a held beam, as every peer learns of it: where it went (from the muzzle to what it hit, or to its
    /// range).
    /// </summary>
    public readonly struct CannonPulseEvent
    {
        public readonly int PulseId;
        public readonly Vector3 Origin;
        public readonly Vector3 End;

        public CannonPulseEvent(int pulseId, Vector3 origin, Vector3 end)
        {
            PulseId = pulseId;
            Origin = origin;
            End = end;
        }
    }

    /// <summary>
    /// A ship cannon: the barrel pivots, the muzzle, the replicated aim and beam state (for peers that do not occupy it)
    /// and the pulse events every peer draws from. The simulation lives in <see cref="CannonFireUseCase"/>; this is
    /// only the thing.
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

        /// <summary>The beam is on (the trigger is held and the cannon is not overheated); written by the server.</summary>
        bool BeamOn { get; }

        /// <summary>0 cold .. 1 overheated; written by the server.</summary>
        float Heat { get; }

        /// <summary>It overheated and vents until it is cold again; written by the server.</summary>
        bool Overheated { get; }

        void ServerSetAim(float yaw, float elevation);
        void ServerSetBeam(bool on, float heat, bool overheated);
        void ServerPulse(int pulseId, Vector3 origin, Vector3 end);

        /// <summary>Every peer: the next pulse received, oldest first.</summary>
        bool TryTakePulse(out CannonPulseEvent pulse);

        /// <summary>Presentation: turns the barrel.</summary>
        void ApplyAim(float yaw, float elevation);

        /// <summary>Presentation: the aiming line for the local occupant (null hides it).</summary>
        void ShowAimPreview(Vector3[]? points, int count);
    }
}
