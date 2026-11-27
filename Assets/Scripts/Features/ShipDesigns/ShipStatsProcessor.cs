#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Pure: a design's mass, lift, thrust and hull (the sums over its known parts; with a cairn, the spirit's lift,
    /// <see cref="ShipStatsTuning.SpiritLift"/>), its maximum health (the hull, at least
    /// <see cref="ShipStatsTuning.MinHealth"/>) and how it flies: its core part's handling profile. Parts do not change
    /// the handling for now (plan .docs/plans/ship-handling-profiles.md). The frame adds mass and hull by plank area and
    /// beam length (<see cref="ShipFrameTuning"/>).
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
            bool cairn = false;
            ShipHandlingProfile? handling = null;
            foreach (var placement in design.Parts)
            {
                if (!catalog.TryGet(placement.PartId, out var part)) continue;
                var stats = part.Stats;
                mass += stats.Mass;
                lift += stats.Lift;
                if (part.IsCairn) cairn = true;
                if (part.IsCore && handling == null) handling = part.Handling;
                thrust += stats.Thrust;
                hull += stats.Hull;
            }

            if (cairn) lift += tuning.SpiritLift;
            return new ShipStats(mass, lift, thrust, hull, Mathf.Max(tuning.MinHealth, hull), handling);
        }
    }
}
