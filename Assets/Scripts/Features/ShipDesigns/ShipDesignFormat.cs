#nullable enable
namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The design file format. Every file carries <c>formatVersion</c>. A change to the format raises
    /// <see cref="CurrentVersion"/>, adds one <see cref="IShipDesignMigration"/> from the old version, and commits a sample of
    /// the old format under <c>Assets/Tests/EditMode/ShipDesigns/</c> that must keep loading.
    /// </summary>
    public static class ShipDesignFormat
    {
        public const int CurrentVersion = 1;
        public const string FileExtension = ".ship.json";

        public const string FormatVersionField = "formatVersion";
        public const string NameField = "name";
        public const string AuthorField = "author";
        public const string NextPartInstanceIdField = "nextPartInstanceId";
        public const string PartsField = "parts";

        public const string PartInstanceIdField = "id";
        public const string PartIdField = "part";
        public const string PartCellField = "cell";
        public const string PartOrientationField = "rot";
    }
}
