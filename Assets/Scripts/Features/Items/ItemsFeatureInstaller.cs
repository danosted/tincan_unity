#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Items
{
    /// <summary>
    /// Items and equipment: the catalog of every item the game knows (so held items travel as ids) and the binder
    /// that turns a held item into granted abilities and tags. Foundational: Fuel (jerry can), FlyingCan (net) and
    /// later tools depend on it, so it runs early and belongs in Profile_Base.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemsFeatureInstaller", menuName = "TinCan/Features/Items Feature Installer")]
    public class ItemsFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Every item that can be held. An item missing here cannot be equipped.")]
        [SerializeField] private List<ItemDefinition> _items = new();

        public override int Order => -15;

        public override void Install(IContainerBuilder builder)
        {
            var catalog = new ItemCatalog(_items);
            foreach (var problem in catalog.Problems) Debug.LogWarning($"[{name}] {problem}", this);

            builder.RegisterInstance(catalog);
            builder.Register<EquipmentAbilityBinder>(Lifetime.Singleton);
        }
    }
}
