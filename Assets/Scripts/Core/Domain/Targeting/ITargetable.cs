#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using UnityEngine;

namespace TinCan.Core.Domain.Targeting
{
    /// <summary>
    /// Something an actor can aim at: a ship part, later interactables, cans, ships. Components implement it;
    /// <c>ActorOrchestrator</c> registers them when their hierarchy spawns (never self-registered). A targetable
    /// with a GAS <see cref="Controller"/> can be filtered by its gameplay tags (for example only <c>State.Damaged</c>).
    /// </summary>
    public interface ITargetable
    {
        Guid TargetId { get; }

        /// <summary>World-space point that shapes measure against (a part's marker, an object's centre).</summary>
        Vector3 AimPoint { get; }

        /// <summary>False while the object should be ignored entirely (despawning, hidden).</summary>
        bool IsTargetable { get; }

        IAbilityControllerBase? Controller { get; }
    }

    /// <summary>Every live targetable, filled by <c>ActorOrchestrator</c> from spawned hierarchies.</summary>
    public interface ITargetableRegistry
    {
        IReadOnlyCollection<ITargetable> All { get; }
        void Register(ITargetable targetable);
        void Unregister(ITargetable targetable);
    }

    /// <summary>
    /// Where an actor aims from, in world space on this peer. Built from simulated state that owner and server share
    /// (the body pose follows the replicated input), so both peers run the same query and the server's answer is
    /// authoritative. <see cref="AimPitch"/> comes from the replicated input (degrees, positive looks down); null for
    /// targeters without one. <see cref="OrbitHeight"/> is where a third-person camera looks through, above the body.
    /// </summary>
    public readonly struct TargetingOrigin
    {
        public readonly Vector3 BodyPosition;
        public readonly Quaternion BodyRotation;
        public readonly float EyeHeight;
        public readonly float? AimPitch;
        public readonly float OrbitHeight;

        public TargetingOrigin(Vector3 bodyPosition, Quaternion bodyRotation, float eyeHeight, float? aimPitch = null, float orbitHeight = 0f)
        {
            BodyPosition = bodyPosition;
            BodyRotation = bodyRotation;
            EyeHeight = eyeHeight;
            AimPitch = aimPitch;
            OrbitHeight = orbitHeight;
        }

        public Vector3 Up => BodyRotation * Vector3.up;
        public Vector3 Eye => BodyPosition + Up * EyeHeight;
        public Vector3 Forward => BodyRotation * Vector3.forward;
        public Vector3 OrbitCentre => BodyPosition + Up * OrbitHeight;

        /// <summary>The body's facing pitched by the aim (level when there is no pitch).</summary>
        public Vector3 AimDirection => BodyRotation * Quaternion.Euler(AimPitch ?? 0f, 0f, 0f) * Vector3.forward;

        /// <summary>Elevation of the aim above the body's horizontal plane, in degrees (up is positive).</summary>
        public float AimElevation => -(AimPitch ?? 0f);
    }

    /// <summary>Something that aims: a player, later AI or a ship turret.</summary>
    public interface ITargeter
    {
        Guid Id { get; }
        bool TryGetOrigin(out TargetingOrigin origin);
    }
}
