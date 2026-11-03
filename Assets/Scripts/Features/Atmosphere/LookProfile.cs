#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// A look, made of parts: its light and air (<see cref="LookSettings"/>), its shading (<see cref="ShadingSettings"/>),
    /// its surface style (<see cref="SurfaceStyle"/>, none for flat colour) and its post effects (a volume profile: the
    /// wide bloom that gives glows their halo, and later the grading). Built by TinCan > Dev > Look > Build Atmosphere from
    /// the palette; the play scenes load it through the Atmosphere installer; look sweeps (TinCan > Dev > Look > Sweeps)
    /// compare variants of it and write the chosen one back. Plans: .docs/plans/look-slice.md (L2),
    /// .docs/plans/artstyle-pipeline-breakdown.md.
    /// </summary>
    [CreateAssetMenu(fileName = "Look", menuName = "TinCan/Look/Look Profile")]
    public class LookProfile : ScriptableObject
    {
        public LookSettings Settings = LookSettings.Default;

        public ShadingSettings Shading = ShadingSettings.Default;

        [Tooltip("How surfaces are painted on top of their palette colour. None: flat colour.")]
        public SurfaceStyle? Surface;

        [Tooltip("Post effects for this look, applied by a global volume while the look is active.")]
        public VolumeProfile? Post;

        /// <summary>The surface style in play: the named one, or flat colour.</summary>
        public ISurfaceStyle ActiveSurface => Surface != null ? Surface : FlatSurfaceStyle.Instance;
    }
}
