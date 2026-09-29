#nullable enable
using System.Collections.Generic;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Core.Items
{
    /// <summary>
    /// What an item is: its network identity, how it looks in the player's hands, and what holding it grants.
    /// Equipping applies <see cref="EquippedEffect"/> (typically an Infinite effect granting tags such as
    /// <c>Item.Tool.Repair</c>) and grants <see cref="GrantedAbilities"/>; unequipping revokes exactly those.
    /// A feature contributes its items through <c>FeatureInstaller.IExtension&lt;ItemDefinition&gt;</c>, so an item
    /// exists only where the feature that makes it work is loaded.
    /// </summary>
    [CreateAssetMenu(fileName = "ITEM_New", menuName = "TinCan/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable network id, unique across all items and never 0 (0 means empty hands). Do not change it once used.")]
        [SerializeField] private int _id;
        [SerializeField] private string _displayName = string.Empty;
        [Tooltip("Shown in the holder's hands while held: instantiated under the player's Visual, keeping the prefab root's local pose. Plain presentation, not networked.")]
        [SerializeField] private GameObject? _heldVisual;
        [SerializeField] private List<AbilityDefinition> _grantedAbilities = new();
        [Tooltip("Applied to the holder while equipped; its granted tags describe what the holder carries.")]
        [SerializeField] private GameplayEffectDefinition? _equippedEffect;

        public int Id => _id;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public GameObject? HeldVisual => _heldVisual;
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => _grantedAbilities;
        public GameplayEffectDefinition? EquippedEffect => _equippedEffect;

        /// <summary>Test and tooling seam; assets set these in the Inspector.</summary>
        public static ItemDefinition Create(int id, string name, GameplayEffectDefinition? equippedEffect = null, params AbilityDefinition[] abilities)
        {
            var item = CreateInstance<ItemDefinition>();
            item.name = name;
            item._id = id;
            item._equippedEffect = equippedEffect;
            item._grantedAbilities = new List<AbilityDefinition>(abilities);
            return item;
        }
    }
}
