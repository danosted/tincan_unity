#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using TinCan.Features.Interaction;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Stations
{
    /// <summary>
    /// Stations a player occupies while staying in their body (<see cref="IStation"/>): the occupancy use case, the
    /// IA_Occupy* handler, "Interact again to leave", and the local occupant looking through the station's camera.
    /// Features that ship stations (the cannon) need it in the profile.
    /// </summary>
    [CreateAssetMenu(fileName = "StationsFeatureInstaller", menuName = "TinCan/Features/Stations Feature Installer")]
    public class StationsFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.Register<StationOccupancyUseCase>(Lifetime.Singleton).As<IStationOccupancy>().As<IInteractOverride>().As<ISimulationTickable>();
            builder.Register<OccupyStationInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
            builder.Register<StationViewPresenter>(Lifetime.Singleton).As<ITickable>().As<ILocalViewOverride>();
        }
    }
}
