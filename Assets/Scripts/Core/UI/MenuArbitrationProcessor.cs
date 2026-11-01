#nullable enable
using System.Collections.Generic;

namespace TinCan.Core.UI
{
    /// <summary>Which open request is shown: Modal beats Screen beats Panel, and within a kind the newest wins.</summary>
    public static class MenuArbitrationProcessor
    {
        /// <summary>The index of the request on top, or -1 when there is none.</summary>
        public static int Top(IReadOnlyList<MenuRequestRank> requests)
        {
            int top = -1;
            for (int i = 0; i < requests.Count; i++)
            {
                if (top < 0 || Beats(requests[i], requests[top])) top = i;
            }
            return top;
        }

        private static bool Beats(MenuRequestRank candidate, MenuRequestRank current) =>
            candidate.Kind != current.Kind ? candidate.Kind > current.Kind : candidate.Sequence > current.Sequence;
    }
}
