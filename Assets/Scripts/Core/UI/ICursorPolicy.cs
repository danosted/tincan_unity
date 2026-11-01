#nullable enable
namespace TinCan.Core.UI
{
    /// <summary>
    /// The one owner of the cursor. Features ask for a mode under their name instead of writing Unity's Cursor (an
    /// architecture rule checks this); <see cref="CursorPolicyProcessor"/> decides between the asks.
    /// </summary>
    public interface ICursorPolicy
    {
        /// <summary>The mode last applied to the cursor.</summary>
        CursorState Mode { get; }

        /// <summary>Asks for a mode; the newest ask wins. Asking again for the same mode keeps its place.</summary>
        void Request(string owner, CursorState mode);
        void Release(string owner);
    }
}
