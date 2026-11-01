#nullable enable
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// A look: its light and air (<see cref="LookSettings"/>) and its post effects (a volume profile: the wide bloom
    /// that gives glows their halo, and later the grading). Built by TinCan > Dev > Look > Build Atmosphere from the
    /// palette; the play scenes load it through the Atmosphere installer. Plan: .docs/plans/look-slice.md (L2).
    /// </summary>
    [CreateAssetMenu(fileName = "Look", menuName = "TinCan/Look/Look Profile")]
    public class LookProfile : ScriptableObject
    {
        public LookSettings Settings = LookSettings.Default;

        [Tooltip("Post effects for this look, applied by a global volume while the look is active.")]
        public VolumeProfile? Post;
    }
}
