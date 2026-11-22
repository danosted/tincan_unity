#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Where a campfire or tent would go along a player's aim, and whether it fits: the server's check on a press and the
    /// local preview ask the same, so the preview's colour is the answer the press gets.
    /// </summary>
    public sealed class CampPlacementFinder
    {
        private readonly ICampPlacementQuery _query;
        private readonly CampingConfig _config;

        public CampPlacementFinder(ICampPlacementQuery query, CampingConfig config)
        {
            _query = query;
            _config = config;
        }

        public CampPlacement Find(CampPlaceable kind, Vector3 eye, Vector3 direction, Transform body)
        {
            if (!_query.TryAim(eye, direction, _config.PlaceReach, body, out var ground))
                return new CampPlacement(kind, CampPlacementVerdict.NoGroundInReach, default, Quaternion.identity);

            float radius = _config.RadiusOf(kind);
            var verdict = CampPlacementProcessor.Judge(ground, _config.MaxSlopeDegrees,
                () => _query.HasRoom(ground.Point, radius, _config.ClearHeight, body));
            return new CampPlacement(kind, verdict, ground.Point, CampPlacementProcessor.FacingBack(eye, ground.Point));
        }
    }
}
