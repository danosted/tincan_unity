#nullable enable
namespace TinCan.Core.Input
{
    /// <summary>Where the player's binding overrides (Input System JSON) are kept between sessions.</summary>
    public interface IInputBindingStore
    {
        string? Load();
        void Save(string overridesJson);
    }
}
