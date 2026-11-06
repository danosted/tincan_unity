#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Rows that come from data and change while a menu is open (games on this network). An authored
    /// <see cref="MenuItemKind.Rows"/> placeholder names the source by <see cref="SourceId"/>; <see cref="MenuUseCase"/>
    /// expands it, rebuilds on <see cref="Changed"/>, and passes a picked row back. Register one with
    /// <c>.As&lt;IMenuRowSource&gt;()</c>.
    /// </summary>
    public interface IMenuRowSource
    {
        string SourceId { get; }

        IReadOnlyList<MenuSourceRow> Rows { get; }

        /// <summary>The note shown instead of rows while there are none ("Looking for games..."); empty shows nothing.</summary>
        string EmptyLabel { get; }

        event Action? Changed;

        /// <summary>Whether a menu on screen shows this source; a source may do its work only while it is shown.</summary>
        void SetShown(bool shown);

        /// <summary>The row with this <see cref="MenuSourceRow.Key"/> was picked in the menu.</summary>
        void Invoke(string key, MenuContext context);
    }
}
