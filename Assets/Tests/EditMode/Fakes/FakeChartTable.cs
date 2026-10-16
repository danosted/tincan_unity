#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Gas;
using TinCan.Core.Humanoid;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using TinCan.Features.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A chart table on a given ship; set <see cref="OccupantClientId"/> to seat a navigator.</summary>
    public sealed class FakeChartTable : IChartTable
    {
        public FakeChartTable(IAirshipView? ship = null) => Ship = ship;

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public IAirshipView? Ship { get; set; }
        public Transform? Chart { get; set; }
        public Transform? Seat => null;
        public AbilityDefinition? OccupyAbility => null;
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => Array.Empty<AbilityDefinition>();
        public Camera? ViewCamera => null;
        public ulong? OccupantClientId { get; set; }
        public InteractionDefinition Definition => null!;

        public void ServerSetOccupant(IHumanoidCharacterView? occupant) => OccupantClientId = occupant != null ? 1UL : null;
    }
}
