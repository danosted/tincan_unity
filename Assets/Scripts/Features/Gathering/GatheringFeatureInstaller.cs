#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Gathering
{
    /// <summary>
    /// Gathering: fuel crystals mined from veins in island rock (held Interact; the veins are placed and spawned by Island
    /// Details), carried one at a time, and stored aboard in a crystal bin (a socket fitting, stores.crystal_bin: a core
    /// item stockpile). Brings the crystal item; feeding it to the engine is Fuel's. Built by TinCan > Dev > Gathering >
    /// Build Assets. Plan: .docs/plans/fuel-gathering.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GatheringFeatureInstaller", menuName = "TinCan/Features/Gathering Feature Installer")]
    public class GatheringFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ItemDefinition>, FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [Tooltip("The items this feature owns (the fuel crystal).")]
        [SerializeField] private List<ItemDefinition> _items = new();

        [Tooltip("The fittings this feature owns (the crystal bin).")]
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<MineCrystalInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs =>
            _fittings.Where(fitting => fitting != null && fitting.Prefab != null).Select(fitting => fitting.Prefab!);

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
