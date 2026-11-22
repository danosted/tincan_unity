#nullable enable

namespace TinCan.Features.Camping
{
    /// <summary>Whether a campfire or tent fits where the player aims, and if not, why.</summary>
    public enum CampPlacementVerdict
    {
        Fits,
        NoGroundInReach,
        TooSteep,
        OnTheShip,
        NoRoom
    }
}
