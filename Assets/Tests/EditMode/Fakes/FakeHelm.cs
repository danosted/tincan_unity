#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using TinCan.Features.Helm;
using TinCan.Features.Stations;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A helm station on a given ship, with no seat or occupy ability (occupancy is faked alongside it).</summary>
    public sealed class FakeHelm : IHelm, IStation
    {
        public FakeHelm(IAirshipView? ship = null) => Ship = ship;

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public IAirshipView? Ship { get; set; }
        public Transform? Seat => null;
        public AbilityDefinition? OccupyAbility => null;
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => Array.Empty<AbilityDefinition>();
        public Camera? ViewCamera => null;
        public ulong? OccupantClientId { get; private set; }
        public InteractionDefinition Definition => null!;

        public void ServerSetOccupant(IHumanoidCharacterView? occupant) => OccupantClientId = occupant != null ? 1UL : null;
    }
}
