#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using TinCan.Features.Atmosphere;
using UnityEngine;

namespace TinCan.DevTools.Editor
{
    /// <summary>What a look sweep renders each variant on.</summary>
    public enum LookSweepSubject
    {
        /// <summary>The Corsair from its three-quarter view, as on the contact sheet.</summary>
        Ship,
        /// <summary>Canvas and timber shapes lit from the side, as on the lab sheet: bands and edges read best here.</summary>
        Shapes,
    }

    /// <summary>
    /// A look sweep (plan .docs/plans/artstyle-pipeline-breakdown.md, P1): a base look and the variants to compare on one
    /// sheet, each tile lettered A, B, C... A variant overrides only what it names, so "B, but warmer" is B's overrides
    /// plus one. Rendered by <see cref="LookSweepSheet"/>; the chosen variant is written back into the base look from the
    /// sweep's inspector. Kept as an asset, so an earlier choice can be rendered again and picked again.
    /// </summary>
    [CreateAssetMenu(fileName = "Sweep", menuName = "TinCan/Look/Look Sweep")]
    public sealed class LookSweep : ScriptableObject
    {
        [Tooltip("The look the variants change; empty: the golden-afternoon look.")]
        public LookProfile? Base;

        [Tooltip("One block of tiles per subject.")]
        public LookSweepSubject[] Subjects = { LookSweepSubject.Ship };

        public List<LookSweepVariant> Variants = new();
    }

    /// <summary>One tile of a sweep: the base look with these overrides.</summary>
    [Serializable]
    public sealed class LookSweepVariant
    {
        [Tooltip("Printed on the tile after its letter; empty: the overridden values.")]
        public string Label = "";

        public bool OverrideBandSoftness;
        [Range(0f, 1f)] public float BandSoftness = 0.35f;
        public bool OverrideBands;
        [Range(2, 3)] public int Bands = 2;
        public bool OverrideRim;
        [Range(0f, 1f)] public float Rim = 0.15f;
        public bool OverrideShadowFill;
        [Range(0f, 1f)] public float ShadowFill = 0.45f;

        public bool OverrideSurface;
        [Tooltip("None: flat colour.")]
        public SurfaceStyle? Surface;

        /// <summary>The base shading with this variant's overrides.</summary>
        public ShadingSettings ShadingOver(ShadingSettings shading)
        {
            if (OverrideBandSoftness) shading.BandSoftness = BandSoftness;
            if (OverrideBands) shading.Bands = Bands;
            if (OverrideRim) shading.Rim = Rim;
            if (OverrideShadowFill) shading.ShadowFill = ShadowFill;
            return shading;
        }

        /// <summary>The base surface style, or this variant's (flat when it names none).</summary>
        public ISurfaceStyle SurfaceOver(ISurfaceStyle surface) =>
            !OverrideSurface ? surface : Surface != null ? Surface : FlatSurfaceStyle.Instance;

        /// <summary>What the tile says after its letter: the label, or the overridden values; "base" when nothing is.</summary>
        public string Describe()
        {
            if (!string.IsNullOrWhiteSpace(Label)) return Label;
            var parts = new List<string>();
            if (OverrideBandSoftness) parts.Add("soft " + BandSoftness.ToString("0.###", CultureInfo.InvariantCulture));
            if (OverrideBands) parts.Add($"bands {Bands}");
            if (OverrideRim) parts.Add("rim " + Rim.ToString("0.##", CultureInfo.InvariantCulture));
            if (OverrideShadowFill) parts.Add("fill " + ShadowFill.ToString("0.##", CultureInfo.InvariantCulture));
            if (OverrideSurface) parts.Add(Surface != null ? Surface.name : "flat");
            return parts.Count > 0 ? string.Join(" ", parts) : "base";
        }

        /// <summary>A tile's letter: A to Z, then AA, AB...</summary>
        public static string Letter(int index) =>
            index < 26 ? ((char)('A' + index)).ToString() : Letter(index / 26 - 1) + (char)('A' + index % 26);
    }
}
