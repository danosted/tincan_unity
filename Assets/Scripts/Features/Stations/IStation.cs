#nullable enable
using System.Collections.Generic;
using TinCan.Features.Abilities;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Interaction;
using UnityEngine;

namespace TinCan.Features.Stations
{
    /// <summary>
    /// Something a player occupies while staying in their own body: a cannon today, maybe the helm later. Occupying is
    /// GAS state on the player, not possession: <see cref="OccupyAbility"/> runs on the player for as long as they sit
    /// here (its active effect locks walking and names the station in a tag) and <see cref="GrantedAbilities"/> are theirs
    /// until they leave. The player's own input stream keeps driving everything, so it stays predicted and tick-exact.
    /// Plan: .docs/plans/cannon-and-hazards.md.
    /// </summary>
    public interface IStation : IInteractionTarget
    {
        /// <summary>Where the occupant stands while occupying.</summary>
        Transform? Seat { get; }

        /// <summary>Runs on the occupant while they occupy (GA_Occupy*); its ActiveEffect holds the occupying state.</summary>
        AbilityDefinition? OccupyAbility { get; }

        /// <summary>Abilities the occupant has only while occupying (GA_FireCannon).</summary>
        IReadOnlyList<AbilityDefinition> GrantedAbilities { get; }

        /// <summary>What the occupant looks through while occupying (off for everyone else); null keeps the body's camera.</summary>
        Camera? ViewCamera { get; }

        /// <summary>The occupant's client id, replicated; null when free.</summary>
        ulong? OccupantClientId { get; }

        /// <summary>Server: records who occupies the station (null frees it).</summary>
        void ServerSetOccupant(IHumanoidCharacterView? occupant);
    }
}
