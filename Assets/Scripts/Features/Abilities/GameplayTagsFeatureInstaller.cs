#nullable enable
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Registers <see cref="IGameplayTagRegistry"/> from the project's <see cref="GameplayTagDatabase"/>. Runs early
    /// because other features resolve tags by name (network tag requests, items, scenarios). Without it the ability
    /// mediator falls back to scanning loaded tag assets. Also registers <see cref="ActorAbilityGrantUseCase"/>, the
    /// socket through which features grant abilities to every humanoid and airship.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagsFeatureInstaller", menuName = "TinCan/Features/Gameplay Tags Installer")]
    public class GameplayTagsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private GameplayTagDatabase? _database;

        public override int Order => -20;

        public override void Install(IContainerBuilder builder)
        {
            // The actor ability socket lives with the tag registry because every profile loads this installer.
            builder.Register<ActorAbilityGrantUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>();

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
