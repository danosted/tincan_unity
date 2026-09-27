#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Abilities.Cues
{
    /// <summary>
    /// A feature-contributed description of what one cue does on every peer (<c>Assets/Abilities/Cues/GCN_*.asset</c>).
    /// Features list their notifies through <c>FeatureInstaller.IExtension&lt;GameplayCueNotify&gt;</c>, so a cue exists
    /// only while its feature is in the scene's profile (<see cref="GameplayCueCatalog"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "GCN_New", menuName = "TinCan/Abilities/Gameplay Cue Notify")]
    public class GameplayCueNotify : ScriptableObject
    {
        [Tooltip("The Cue.* gameplay tag this notify presents.")]
        [SerializeField] private GameplayTag? _cue;

        public GameplayTag? Cue => _cue;
    }
}
