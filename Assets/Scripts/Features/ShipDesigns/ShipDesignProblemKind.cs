#nullable enable
namespace TinCan.Features.ShipDesigns
{
    public enum ShipDesignProblemKind
    {
        /// <summary>The part type is not loaded. Kept in the design, skipped when building; the only non-blocking kind.</summary>
        UnknownPart,
        DuplicateInstanceId,
        InvalidInstanceId,
        InvalidOrientation,
        OutOfBounds,
        Overlap,
        Disconnected,
        MissingCore,
        ExtraCore,
        TooManyParts,
        NameTooLong,
        /// <summary>Lift is below mass: the ship cannot hold itself up.</summary>
        TooHeavy,
        /// <summary>No part gives thrust: the ship cannot move.</summary>
        NoThrust,
    }
}
