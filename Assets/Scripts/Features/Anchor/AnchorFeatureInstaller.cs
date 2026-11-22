#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// The anchor: a socket fitting (ship.anchor) the crew drops so the ship waits for them while they are away from the
    /// helm. Built by TinCan > Dev > Anchor > Build Assets. Plan: .docs/plans/peaceful-mode.md (P1).
    /// </summary>
    [CreateAssetMenu(fileName = "AnchorFeatureInstaller", menuName = "TinCan/Features/Anchor Feature Installer")]
    public class AnchorFeatureInstaller : FeatureInstaller, FeatureInstaller.IExtension<ShipFittingDefinition>
    {
        [SerializeField] private List<ShipFittingDefinition> _fittings = new();

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<AnchorHoldUseCase>(Lifetime.Singleton).As<IAirshipHold>();
            builder.Register<ToggleAnchorInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        }

        public override IEnumerable<GameObject> NetworkedPrefabs =>
            _fittings.Where(fitting => fitting != null && fitting.Prefab != null).Select(fitting => fitting.Prefab!);

        IEnumerable<ShipFittingDefinition> FeatureInstaller.IExtension<ShipFittingDefinition>.Contributions => _fittings;
    }
}
