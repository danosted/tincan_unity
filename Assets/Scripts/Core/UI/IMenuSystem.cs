#nullable enable
using System;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Headless menu API. Features ask for menus with <see cref="Request"/> (one request per owner, each with its own
    /// stack of submenus); the request on top (<see cref="MenuArbitrationProcessor"/>) is <see cref="Current"/>. Views
    /// subscribe to <see cref="Changed"/> and render it; gameplay code never talks to a view directly.
    /// </summary>
    public interface IMenuSystem
    {
        /// <summary>The top request's top menu, null when nothing is requested.</summary>
        MenuSnapshot? Current { get; }
        bool IsOpen { get; }
        event Action? Changed;

        /// <summary>
        /// Opens the owner's request, or replaces it when its menu id, kind or pin differ (a replaced request counts as
        /// new). Asking again for the same menu changes nothing, so a use case may request every frame.
        /// </summary>
        void Request(MenuRequest request);
        /// <summary>Closes the owner's request, pinned or not.</summary>
        void Withdraw(string owner);
        bool IsRequested(string owner);

        /// <summary>Shim for older callers: an unpinned Screen request owned by the menu's id.</summary>
        void Open(MenuDefinition menu);
        /// <summary>
        /// Steps back inside the top request: pops a submenu, or closes an unpinned request at its root. False (and no
        /// change) when nothing is open or the top request is pinned at its root.
        /// </summary>
        bool Back();
        /// <summary>Closes every unpinned request.</summary>
        void CloseAll();
        void Invoke(string itemId);
        void SetValue(string itemId, string value);
        string GetValue(string itemId);
    }
}
