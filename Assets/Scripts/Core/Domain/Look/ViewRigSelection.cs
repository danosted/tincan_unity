#nullable enable
using System.Collections.Generic;

namespace TinCan.Core.Domain.Look
{
    /// <summary>
    /// Picks the camera rig: the first registered one. Not enforced, so a profile with two rigs still runs, but it says
    /// so, and a profile with none leaves the camera where the prefab puts it.
    /// </summary>
    public static class ViewRigSelection
    {
        public static IViewRig? Select(IReadOnlyList<IViewRig>? rigs, out string? warning)
        {
            int count = rigs?.Count ?? 0;
            warning = count switch
            {
                0 => "No camera rig registered (add a camera feature such as ThirdPersonCamera to the profile); the camera stays where the prefab puts it.",
                1 => null,
                _ => $"{count} camera rigs registered; using the first ({rigs![0].GetType().Name}). Keep one camera feature in the profile."
            };
            return count > 0 ? rigs![0] : null;
        }
    }
}
