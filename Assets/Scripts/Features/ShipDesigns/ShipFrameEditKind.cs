#nullable enable
namespace TinCan.Features.ShipDesigns
{
    public enum ShipFrameEditKind
    {
        /// <summary>A new story on top, of the given height.</summary>
        AddStory,
        AddNode,
        MoveNode,
        /// <summary>Removes a node with its edges and the faces it is a corner of.</summary>
        RemoveNode,
        /// <summary>Folds one node into another on the same story: its edges and face corners move onto the kept node.</summary>
        MergeNodes,
        AddEdge,
        /// <summary>Removes an edge; a face it bounds goes with it, and two faces it divides become one.</summary>
        RemoveEdge,
        /// <summary>Puts a new node on a floor edge, splitting it in two (and adding the corner to the faces it bounds).</summary>
        SplitEdge,
        SetRailing,
        AddFace,
        /// <summary>Divides a face along a line between two of its corners.</summary>
        SplitFace,
        SetFaceFill,
        RemoveFace,
        /// <summary>Puts a whole frame back (undoing any other frame edit).</summary>
        Restore,
        /// <summary>Hangs a flight of stairs from a floor edge, down into the story below.</summary>
        AddStair,
        /// <summary>Turns a flight to the other side of its edge.</summary>
        FlipStair,
        RemoveStair,
    }
}
