#nullable enable
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>The placement's tunables (<see cref="IslandDetailsConfig.Rules"/>). Counts per hectare (10,000 m²) of top.</summary>
    public readonly struct IslandDetailRules
    {
        public readonly float TreesPerHectare;
        public readonly int MaxTrees;
        public readonly int MaxTreesOnALandmass;
        public readonly float GroveHectares;
        public readonly float GroveSpread;
        public readonly float TreeSpacing;
        public readonly Vector2 TreeScale;
        public readonly float BouldersPerHectare;
        public readonly int MaxBoulders;
        public readonly float BoulderSpacing;
        public readonly Vector2 BoulderScale;
        public readonly float BushesPerHectare;
        public readonly int MaxBushes;
        public readonly float BushSpacing;
        public readonly Vector2 BushScale;
        public readonly float RimReach;
        public readonly float ClearingRadius;
        public readonly int TreeVariants;
        public readonly int BoulderVariants;
        public readonly int BushVariants;

        public IslandDetailRules(float treesPerHectare, int maxTrees, int maxTreesOnALandmass, float groveHectares,
            float groveSpread, float treeSpacing, Vector2 treeScale, float bouldersPerHectare, int maxBoulders,
            float boulderSpacing, Vector2 boulderScale, float bushesPerHectare, int maxBushes, float bushSpacing,
            Vector2 bushScale, float rimReach, float clearingRadius, int treeVariants, int boulderVariants, int bushVariants)
        {
            TreesPerHectare = treesPerHectare;
            MaxTrees = maxTrees;
            MaxTreesOnALandmass = maxTreesOnALandmass;
            GroveHectares = groveHectares;
            GroveSpread = groveSpread;
            TreeSpacing = treeSpacing;
            TreeScale = treeScale;
            BouldersPerHectare = bouldersPerHectare;
            MaxBoulders = maxBoulders;
            BoulderSpacing = boulderSpacing;
            BoulderScale = boulderScale;
            BushesPerHectare = bushesPerHectare;
            MaxBushes = maxBushes;
            BushSpacing = bushSpacing;
            BushScale = bushScale;
            RimReach = rimReach;
            ClearingRadius = clearingRadius;
            TreeVariants = treeVariants;
            BoulderVariants = boulderVariants;
            BushVariants = bushVariants;
        }

        public float Spacing(IslandDetailKind kind) => kind switch
        {
            IslandDetailKind.Tree => TreeSpacing,
            IslandDetailKind.Boulder => BoulderSpacing,
            _ => BushSpacing
        };
    }
}
