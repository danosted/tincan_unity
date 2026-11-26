#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.UI;
using TinCan.Features.ShipLoss;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// The voyage's row on the Ship Loss Lost screen (its <see cref="ShipLossMenus.ChoicesSource"/>): Restart Voyage,
    /// which asks the server for a new voyage from the voyage's start (a server RPC on a client).
    /// </summary>
    public sealed class VoyageLostChoices : IMenuRowSource
    {
        public const string RestartKey = "restart";

        private static readonly MenuSourceRow[] Restart = { new(RestartKey, "Restart Voyage") };

        private readonly RestartVoyageMenuCommand _restart;

        public VoyageLostChoices(RestartVoyageMenuCommand restart) => _restart = restart;

        public string SourceId => ShipLossMenus.ChoicesSource;
        public IReadOnlyList<MenuSourceRow> Rows => Restart;
        public string EmptyLabel => string.Empty;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public void SetShown(bool shown) { }

        public void Invoke(string key, MenuContext context)
        {
            if (key == RestartKey) _restart.Execute(context);
        }
    }
}
