#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using UnityEngine;
using VContainer;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Items and equipment (core, always loaded): the catalog of every item this scene knows (so held items travel as
    /// ids) and the binder that turns a held item into granted abilities and tags. Items belong to the features that
    /// make them work and arrive through <c>FeatureInstaller.IExtension&lt;ItemDefinition&gt;</c> (Fuel: jerry can,
    /// the net-catch minigame: net, ship damage: repair tool); <see cref="_items"/> is for core items only.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemsFeatureInstaller", menuName = "TinCan/Features/Items Feature Installer")]
    public class ItemsFeatureInstaller : FeatureInstaller
    {
        [Tooltip("Core items only. A feature's item goes on that feature's installer, so it exists only where the feature is loaded.")]
        [SerializeField] private List<ItemDefinition> _items = new();

        public override int Order => -15;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register(resolver => BuildCatalog(resolver.Resolve<FeatureInstallerCatalog>()), Lifetime.Singleton);
            builder.Register<EquipmentAbilityBinder>(Lifetime.Singleton);
            builder.Register<TakeItemInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        }

        private ItemCatalog BuildCatalog(FeatureInstallerCatalog features)
        {
            var contributed = features.Installers
                .OfType<FeatureInstaller.IExtension<ItemDefinition>>()
                .SelectMany(e => e.Contributions);
            var catalog = new ItemCatalog(_items.Concat(contributed));
            foreach (var problem in catalog.Problems) Debug.LogWarning($"[{name}] {problem}", this);
            return catalog;
        }
    }
}
