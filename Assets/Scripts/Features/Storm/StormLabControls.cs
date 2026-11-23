#nullable enable
using TinCan.Core.Domain;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm lab's controls, a component in Lab_Storm only (TinCan > Dev > Storm > Build Lab puts it there): select
    /// "Storm Lab Controls" while it plays and drag the sliders, or switch on Follow Zone and sail into the zone. Without
    /// one (the game), the nearest encounter zone drives the storm (<see cref="StormIntensity"/>). Edits made in Play are
    /// lost when it stops; the storm's tuning is on StormConfig.
    /// </summary>
    public sealed class StormLabControls : MonoBehaviour, IInjectedView
    {
        [Tooltip("How far into the storm's disc: 0 its wall's foot, 0.5 the bands, 1 the eye (calm).")]
        [Range(0f, 1f)] public float Depth = 0.4f;
        [Tooltip("1: raging. 0: cured (the storm comes apart).")]
        [Range(0f, 1f)] public float Strength = 1f;
        [Tooltip("On: the encounter zone drives the storm, as in the game (sail in to see it).")]
        public bool FollowZone;

        /// <summary>Injected by the scope (a scene view): hands these controls to the storm's intensity.</summary>
        [Inject]
        public void Construct(StormIntensity intensity) => intensity.UseLab(this);
    }
}
