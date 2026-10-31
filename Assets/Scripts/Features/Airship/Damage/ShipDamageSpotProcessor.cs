#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// Pure: where a designed ship's hull damage points go. Plank floors only (a wall's centre can be out of a player's
    /// reach), taken story by story from the top down, the largest floor of each story first, so the spots spread over
    /// the decks. The same design always gives the same spots, in the same order.
    /// </summary>
    public sealed class ShipDamageSpotProcessor
    {
        public IReadOnlyList<ShipHullSurface> Pick(IReadOnlyList<ShipHullSurface> surfaces, int count)
        {
            var stories = surfaces
                .Where(surface => !surface.IsWall)
                .GroupBy(surface => surface.Story)
                .OrderByDescending(story => story.Key)
                .Select(story => new Queue<ShipHullSurface>(story.OrderByDescending(surface => surface.Area).ThenBy(surface => surface.FaceId)))
                .ToList();

            var picked = new List<ShipHullSurface>(count);
            while (picked.Count < count && stories.Any(story => story.Count > 0))
            {
                foreach (var story in stories)
                {
                    if (picked.Count == count) break;
                    if (story.Count > 0) picked.Add(story.Dequeue());
                }
            }

            return picked;
        }
    }
}
