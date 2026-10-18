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
        /// <summary>Two parts' blocks overlap.</summary>
        Overlap,
        /// <summary>A piece of frame, or a part on it, is not joined to the frame the core stands on.</summary>
        Disconnected,
        MissingCore,
        ExtraCore,
        TooManyParts,
        NameTooLong,
        /// <summary>Lift is below mass: the ship cannot hold itself up.</summary>
        TooHeavy,
        /// <summary>No part gives thrust: the ship cannot move.</summary>
        NoThrust,
        /// <summary>A frame element names a node or story that is not there.</summary>
        FrameReference,
        /// <summary>An edge joins a node to itself, skips a story, is too short, or doubles another.</summary>
        BadEdge,
        /// <summary>A face is too small, crosses itself, spans stories, has a side that is not an edge, or overlaps another plank face.</summary>
        BadFace,
        /// <summary>A node with fewer than two edges.</summary>
        DanglingNode,
        /// <summary>A node drifts further from its story's floor plane than the tuning allows.</summary>
        DriftOutOfBand,
        /// <summary>A plank floor is steeper than a player can walk.</summary>
        FloorTooSteep,
        /// <summary>Too many stories or frame elements.</summary>
        FrameTooLarge,
        /// <summary>A part's anchor does not hold it: its element is missing or the wrong kind, or its point is off its plank floor.</summary>
        UnanchoredPart,
        /// <summary>A second cairn: a ship has one spirit.</summary>
        ExtraCairn,
        /// <summary>Stairs whose landing edge, foot or hatch do not work.</summary>
        BadStairs,
    }
}
