#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>Writes a cursor mode to the platform (<see cref="UnityCursorDevice"/>); a seam so the policy can be tested.</summary>
    public interface ICursorDevice
    {
        void Apply(CursorState mode);
    }
}
