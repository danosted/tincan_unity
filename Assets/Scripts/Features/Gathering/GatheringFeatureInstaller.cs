#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Gathering
{
    /// <summary>
    /// Gathering: fuel crystals mined from veins in island rock (held Interact; the veins are placed and spawned by Island
    /// Details), carried one at a time. Brings the crystal item; feeding it to the engine is Fuel's. Built by TinCan > Dev
    /// > Gathering > Build Assets. Plan: .docs/plans/fuel-gathering.md.
    /// </summary>
    [CreateAssetMenu(fileName = "GatheringFeatureInstaller", menuName = "TinCan/Features/Gathering Feature Installer")]
    public class GatheringFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ItemDefinition>
    {
        [Tooltip("The items this feature owns (the fuel crystal).")]
        [SerializeField] private List<ItemDefinition> _items = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<MineCrystalInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        }

        IEnumerable<ItemDefinition> FeatureInstaller.IExtension<ItemDefinition>.Contributions => _items;
    }
}
