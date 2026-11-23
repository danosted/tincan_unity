#nullable enable
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship.PhysicalParts
{
    /// <summary>
    /// The airship's doors. Interacting with an <see cref="AirshipDoor"/> toggles it through core's ToggleInteractionHandler
    /// (IA_ToggleDoor), so there is nothing to register; the installer keeps the doors in a profile's list.
    /// </summary>
    [CreateAssetMenu(fileName = "AirshipDoorFeatureInstaller", menuName = "TinCan/Features/Airship Door Feature Installer")]
    public class AirshipDoorFeatureInstaller : FeatureInstaller
    {
        public override void Install(IContainerBuilder builder) { }
    }
}
