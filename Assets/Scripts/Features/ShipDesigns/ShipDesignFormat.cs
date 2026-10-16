#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The design file format. Every file carries <c>formatVersion</c>. A change to the format raises
    /// <see cref="CurrentVersion"/>, adds one <see cref="IShipDesignMigration"/> from the old version, and commits a sample of
    /// the old format under <c>Assets/Tests/EditMode/ShipDesigns/</c> that must keep loading.
    /// Version 2 (.docs/plans/ship-frames.md): the frame, and <c>nextId</c> (shared by parts and frame elements) in place of
    /// <c>nextPartInstanceId</c>.
    /// </summary>
    public static class ShipDesignFormat
    {
        public const int CurrentVersion = 2;
        public const string FileExtension = ".ship.json";

        public const string FormatVersionField = "formatVersion";
        public const string NameField = "name";
        public const string AuthorField = "author";
        public const string NextIdField = "nextId";
        public const string PartsField = "parts";
        public const string FrameField = "frame";

        /// <summary>Version 1's name for <see cref="NextIdField"/>.</summary>
        public const string V1NextPartInstanceIdField = "nextPartInstanceId";

        public const string PartInstanceIdField = "id";
        public const string PartIdField = "part";
        public const string PartCellField = "cell";
        public const string PartOrientationField = "rot";

        public const string FrameBaseField = "base";
        public const string FrameStoriesField = "stories";
        public const string FrameNodesField = "nodes";
        public const string FrameEdgesField = "edges";
        public const string FrameFacesField = "faces";

        public const string ElementIdField = "id";
        public const string NodeStoryField = "story";
        public const string NodePositionField = "pos";
        public const string EdgeAField = "a";
        public const string EdgeBField = "b";
        public const string EdgeRailingField = "rail";
        public const string FaceNodesField = "nodes";
        public const string FaceFillField = "fill";

        public const string FillOpen = "open";
        public const string FillPlanks = "planks";
    }
}
