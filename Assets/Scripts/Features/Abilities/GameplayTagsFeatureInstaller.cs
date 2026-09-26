#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Registers <see cref="IGameplayTagRegistry"/> from the project's <see cref="GameplayTagDatabase"/>. Runs early
    /// because other features resolve tags by name (network tag requests, items, scenarios). Without it the ability
    /// mediator falls back to scanning loaded tag assets.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagsFeatureInstaller", menuName = "TinCan/Features/Gameplay Tags Installer")]
    public class GameplayTagsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private GameplayTagDatabase? _database;

        public override int Order => -20;

        public override void Install(IContainerBuilder builder)
        {
            if (_database == null)
            {
                Debug.LogWarning($"[{name}] No GameplayTagDatabase assigned; tag lookups fall back to scanning loaded assets.", this);
                return;
            }

            var registry = new GameplayTagRegistry(_database.Tags);
            if (registry.Duplicates.Count > 0)
            {
                Debug.LogWarning($"[{name}] Duplicate gameplay tag names (first wins): {string.Join(", ", registry.Duplicates)}", this);
            }

            builder.RegisterInstance<IGameplayTagRegistry>(registry);
        }
    }
}
