#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace TinCan.Core.Infrastructure.Extensions
{
    public static class IObjectResolverExtensions
    {
        /// <summary>
        /// Ensures that a networked prefab is properly injected with VContainer dependencies when instantiated by NGO.
        /// Also allows for custom initialization and cleanup logic via configureInit and configureDestroy callbacks.
        /// </summary>
        public static void AddNetworkedPrefab(
            this IObjectResolver resolver,
            NetworkManager networkManager,
            GameObject prefab,
            Action<GameObject, ulong>? configureInit = null,
            Action<GameObject>? configureDestroy = null,
            Action? onServerStarted = null)
        {
            if (networkManager is null) return;
            if (prefab is null) return;

            var prefabInterceptor = new NetworkPrefabInterceptor(resolver, prefab, configureInit, configureDestroy);
            networkManager.PrefabHandler.AddHandler(prefab, prefabInterceptor);

            if (onServerStarted is not null)
            {
                networkManager.OnServerStarted += onServerStarted;
            }
        }
    }
}
