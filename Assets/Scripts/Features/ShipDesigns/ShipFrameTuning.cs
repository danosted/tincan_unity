#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Frame rules and looks: how far nodes may drift and how steep a floor may be (the validator), how planks and beams
    /// are built (the mesh processor), and what they weigh (the stats). Plan: .docs/plans/ship-frames.md.
    /// </summary>
    [Serializable]
    public struct ShipFrameTuning
    {
        [Header("Rules")]
        [Tooltip("How far (m) a node may drift above or below its story's floor plane.")]
        [Min(0f)] public float MaxDrift;
        [Tooltip("The steepest a plank floor may tilt (degrees); keep it at or under the player's walkable slope.")]
        [Range(0f, 89f)] public float MaxFloorSlope;
        [Tooltip("The shortest edge (m).")]
        [Min(0.01f)] public float MinEdgeLength;
        [Tooltip("The smallest face, seen from above (m²).")]
        [Min(0.001f)] public float MinFaceArea;
        [Tooltip("How close (m) a part's underside must be to a plank floor to rest on it.")]
        [Min(0f)] public float RestTolerance;
        [Tooltip("The least room (m) between a plank floor and the underside of the floor above it.")]
        [Min(0f)] public float MinHeadroom;

        [Header("Building")]
        [Tooltip("Plank floor thickness (m); a floor's top is where its nodes are.")]
        [Min(0.01f)] public float PlankThickness;
        [Tooltip("Beam width and depth (m).")]
        [Min(0.01f)] public float BeamWidth;
        [Tooltip("Metres of plank or beam per texture repeat.")]
        [Min(0.1f)] public float TextureMetres;
        [Tooltip("How high a railing's top rail is above the floor (m).")]
        [Min(0.3f)] public float RailHeight;

        [Header("Stats")]
        [Min(0f)] public float PlankMassPerSquareMetre;
        [Min(0f)] public float PlankHullPerSquareMetre;
        [Min(0f)] public float BeamMassPerMetre;
        [Min(0f)] public float BeamHullPerMetre;

        // A deck tile part is 1 m², mass 3 and hull 5; frame planks weigh and hold the same per square metre.
        public static ShipFrameTuning Default => new()
        {
            MaxDrift = 0.8f,
            MaxFloorSlope = 30f,
            MinEdgeLength = 0.3f,
            MinFaceArea = 0.25f,
            RestTolerance = 0.25f,
            MinHeadroom = 2.2f,
            PlankThickness = 0.15f,
            BeamWidth = 0.2f,
            TextureMetres = 2f,
            RailHeight = 1f,
            PlankMassPerSquareMetre = 3f,
            PlankHullPerSquareMetre = 5f,
            BeamMassPerMetre = 1f,
            BeamHullPerMetre = 2f,
        };
    }
}
