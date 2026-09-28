#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using TinCan.Features.Interaction;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship
{
    /// <summary>The airship's doors: interacting with an <see cref="AirshipDoor"/> toggles it (IA_ToggleDoor).</summary>
    [CreateAssetMenu(fileName = "AirshipDoorFeatureInstaller", menuName = "TinCan/Features/Airship Door Feature Installer")]
    public class AirshipDoorFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private GameplayTag? _handlerTag;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<DoorInteractionHandler>(Lifetime.Singleton)
                .WithParameter("handlerTag", _handlerTag)
                .As<IInteractionHandler>();
        }
    }
}
