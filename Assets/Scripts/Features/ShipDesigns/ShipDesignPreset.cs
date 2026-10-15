#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// On a ship prefab: the stored design this ship is built from, unless the shipyard's Launch or the
    /// <c>-shipDesign</c> launch argument chooses another (<see cref="ShipDesignUseCase"/>), and the installer fixtures
    /// made for another hull that stay off it (<see cref="ShipDesignPresetFixtureFilter"/>). A ship without one gets the
    /// config's default design. First use: <c>Prefabs/Airship/Galleon_Prefab</c>.
    /// </summary>
    public sealed class ShipDesignPreset : MonoBehaviour
    {
        [Tooltip("The design's key: its file name without .ship.json (a built-in such as Galleon, or a saved design).")]
        [SerializeField] private string _design = string.Empty;
        [Tooltip("Fixed-pose fixtures placed for another hull, kept off this ship.")]
        [SerializeField] private List<ShipFixtureDefinition> _keepOffFixtures = new();

        public string DesignKey => _design;

        public bool KeepsOff(ShipFixtureDefinition fixture) => _keepOffFixtures.Any(f => f == fixture);

        public void Configure(string designKey, IEnumerable<ShipFixtureDefinition> keepOffFixtures)
        {
            _design = designKey;
            _keepOffFixtures = keepOffFixtures.ToList();
        }
    }
}
