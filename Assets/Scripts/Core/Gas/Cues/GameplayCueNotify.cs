#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Core.Gas.Cues
{
    /// <summary>
    /// A feature-contributed description of what one cue does on every peer (<c>Assets/Abilities/Cues/GCN_*.asset</c>):
    /// three action lists, composed in the Inspector from <see cref="GameplayCueAction"/> types.
    /// <list type="bullet">
    /// <item>OnExecute: a burst (an Instant effect's cue).</item>
    /// <item>OnActive: the cue tag appeared; what these actions start lives until the tag goes.</item>
    /// <item>OnRemoved: the cue tag went (not on despawn).</item>
    /// </list>
    /// Features list their notifies through <c>FeatureInstaller.IExtension&lt;GameplayCueNotify&gt;</c>, so a cue exists
    /// only while its feature is in the scene's profile (<see cref="GameplayCueCatalog"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "GCN_New", menuName = "TinCan/Abilities/Gameplay Cue Notify")]
    public class GameplayCueNotify : ScriptableObject
    {
        [Tooltip("The Cue.* gameplay tag this notify presents.")]
        [SerializeField] private GameplayTag? _cue;

        [SerializeReference, GameplayCueActionPicker] private List<GameplayCueAction?> _onExecute = new();
        [SerializeReference, GameplayCueActionPicker] private List<GameplayCueAction?> _onActive = new();
        [SerializeReference, GameplayCueActionPicker] private List<GameplayCueAction?> _onRemoved = new();

        public GameplayTag? Cue => _cue;
        public IReadOnlyList<GameplayCueAction?> OnExecute => _onExecute;
        public IReadOnlyList<GameplayCueAction?> OnActive => _onActive;
        public IReadOnlyList<GameplayCueAction?> OnRemoved => _onRemoved;
    }
}
