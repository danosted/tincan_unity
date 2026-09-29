#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// GAS core, always loaded (a core installer; profiles never list it). Registers <see cref="IGameplayTagRegistry"/>
    /// from the project's <see cref="GameplayTagDatabase"/> and <see cref="ActorAbilityGrantUseCase"/>, the socket
    /// through which features grant abilities to every humanoid and airship. Runs early because other systems resolve
    /// tags by name (network tag replication, items, scenarios).
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayTagsFeatureInstaller", menuName = "TinCan/Features/Gameplay Tags Installer")]
    public class GameplayTagsFeatureInstaller : FeatureInstaller
    {
        [SerializeField] private GameplayTagDatabase? _database;

        public override int Order => -20;

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<ActorAbilityGrantUseCase>(Lifetime.Singleton).AsSelf().As<IInitializable>();

            // The registry is a core service: always registered, so nothing needs to resolve it optionally.
            if (_database == null)
            {
                Debug.LogError($"[{name}] No GameplayTagDatabase assigned; no gameplay tag resolves by name.", this);
            }

            var registry = new GameplayTagRegistry(_database != null ? _database.Tags : (IReadOnlyList<GameplayTag>)Array.Empty<GameplayTag>());
            if (registry.Duplicates.Count > 0)
            {
                Debug.LogWarning($"[{name}] Duplicate gameplay tag names (first wins): {string.Join(", ", registry.Duplicates)}", this);
            }

            builder.RegisterInstance<IGameplayTagRegistry>(registry);
        }
    }
}
