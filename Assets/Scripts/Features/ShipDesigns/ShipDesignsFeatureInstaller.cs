#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship.Fixtures;
using TinCan.Core.Ship.Parts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Ship designs: the design model, its file format (JSON, versioned, migrated) and network form, the part catalog
    /// every loaded feature contributes to, validation, edits, and the store of saved and built-in designs. In play, the
    /// server gives each ship a design (<see cref="ShipDesignUseCase"/>), the ShipDesignState fixture replicates it, and
    /// every peer builds the ship from it (<see cref="ShipAssemblyUseCase"/>). Contributes the generic hull parts;
    /// features contribute their own (the helm). Plan: .docs/plans/modular-airship-builder.md.
    /// </summary>
    [CreateAssetMenu(fileName = "ShipDesignsFeatureInstaller", menuName = "TinCan/Features/Ship Designs Feature Installer")]
    public class ShipDesignsFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipPartDefinition>,
        FeatureInstaller.IExtension<ShipFixtureDefinition>
    {
        [SerializeField] private ShipDesignsConfig? _config;
        [Tooltip("The generic structural parts (hull block, deck).")]
        [SerializeField] private List<ShipPartDefinition> _parts = new();
        [Tooltip("The ShipDesignState fixture: carries each ship's design to every peer.")]
        [SerializeField] private ShipFixtureDefinition? _stateFixture;

        public override void Install(IContainerBuilder builder)
        {
            if (_config == null)
            {
                Debug.LogWarning($"[{name}] No ShipDesignsConfig assigned; there are no ship designs.", this);
                return;
            }

            builder.RegisterInstance(_config);
            builder.Register<ShipPartCatalog>(Lifetime.Singleton).As<IShipPartCatalog>();
            builder.RegisterInstance<IShipDesignCodec>(new ShipDesignJsonCodec(_config.Limits));
            builder.Register<FileShipDesignStore>(Lifetime.Singleton).As<IShipDesignStore>();
            builder.Register<ShipDesignValidator>(Lifetime.Transient);
            builder.Register<ShipDesignEditProcessor>(Lifetime.Transient);
            builder.Register<ShipStatsProcessor>(Lifetime.Transient);

            builder.Register<ShipDesignUseCase>(Lifetime.Singleton).As<IShipDesigns>().As<ITickable>();
            builder.Register<ShipHullBuilder>(Lifetime.Singleton).As<IShipHullBuilder>();
            builder.Register<ShipAssemblyUseCase>(Lifetime.Singleton).As<IShipAssembly>().As<ITickable>();
            builder.Register<ShipDesignFixtureFilter>(Lifetime.Singleton).As<IShipFixtureFilter>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs
        {
            get { if (_stateFixture != null && _stateFixture.Prefab != null) yield return _stateFixture.Prefab; }
        }

        IEnumerable<ShipPartDefinition> FeatureInstaller.IExtension<ShipPartDefinition>.Contributions => _parts;

        IEnumerable<ShipFixtureDefinition> FeatureInstaller.IExtension<ShipFixtureDefinition>.Contributions
        {
            get { if (_stateFixture != null) yield return _stateFixture; }
        }
    }
}
