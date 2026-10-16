#nullable enable
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: a design's mass, lift, thrust and hull (the sums over its known parts), and the flight they give. Top speed
    /// is thrust over mass times <see cref="ShipStatsTuning.SpeedPerThrustOverMass"/>, capped; a ship that cannot lift
    /// itself or has no thrust has none. Turn rate is the base rate scaled by reference mass over mass. Maximum health is
    /// the hull, at least <see cref="ShipStatsTuning.MinHealth"/>. The frame adds mass and hull by plank area and beam length
    /// (<see cref="ShipFrameTuning"/>).
    /// </summary>
    public sealed class ShipStatsProcessor
    {
        public ShipStats Compute(ShipDesign design, IShipPartCatalog catalog, ShipStatsTuning tuning, ShipFrameTuning? frameTuning = null)
        {
            var frame = frameTuning ?? ShipFrameTuning.Default;
            var (plankArea, beamLength) = design.Frame.IsEmpty ? (0f, 0f) : ShipFrameShape.From(design.Frame).Totals();
            float mass = plankArea * frame.PlankMassPerSquareMetre + beamLength * frame.BeamMassPerMetre;
            float hull = plankArea * frame.PlankHullPerSquareMetre + beamLength * frame.BeamHullPerMetre;
            float lift = 0f, thrust = 0f;
            foreach (var placement in design.Parts)
            {
                if (!catalog.TryGet(placement.PartId, out var part)) continue;
                var stats = part.Stats;
                mass += stats.Mass;
                lift += stats.Lift;
                thrust += stats.Thrust;
                hull += stats.Hull;
            }

            bool flies = lift >= mass && thrust > 0f && mass > 0f;
            float speed = flies ? Mathf.Min(tuning.MaxSpeed, tuning.SpeedPerThrustOverMass * thrust / mass) : 0f;
            float turnScale = mass > 0f
                ? Mathf.Clamp(tuning.ReferenceMass / mass, tuning.TurnScaleRange.x, tuning.TurnScaleRange.y)
                : tuning.TurnScaleRange.y;
            float turn = flies ? tuning.BaseTurnSpeed * turnScale : 0f;
            return new ShipStats(mass, lift, thrust, hull, speed, turn, Mathf.Max(tuning.MinHealth, hull));
        }
    }
}
