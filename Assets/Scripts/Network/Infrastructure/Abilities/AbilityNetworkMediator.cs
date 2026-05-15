#nullable enable
using Unity.Netcode;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using System.Collections.Generic;
using System;
using VContainer;
using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Network.Infrastructure.Abilities
{
    /// <summary>
    /// NGO Mediator for the Ability System.
    /// Syncs tags and handles state synchronization for abilities.
    /// </summary>
    public class AbilityNetworkMediator : NetworkMediator, IAbilityControllerBase
    {
        private AbilitySystemUseCase _abilitySystem = null!; // Injected
        private GameplayTagContainer _activeTags = new GameplayTagContainer(null);
        private readonly HashSet<string> _clientActiveTagNames = new();
        private readonly Dictionary<Type, IAttributeSet> _attributeSets = new();
        private NetworkList<NetworkedAttribute> _networkedAttributes = null!; // Initialized in Awake

        // Shadow dictionary for local prediction and fast access
        private readonly Dictionary<int, AttributeValue> _localAttributes = new();

        // Implement IActor to return the parent's ID, solving the identity mismatch
        public override Guid Id
        {
            get
            {
                var parentActor = GetComponentInParent<IActor>();
                if (parentActor != null && (object)parentActor != this)
                {
                    return parentActor.Id;
                }
                return base.Id;
            }
        }

        private void Awake()
        {
            _networkedAttributes = new NetworkList<NetworkedAttribute>();
        }
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _networkedAttributes.OnListChanged += OnNetworkedAttributesChanged;

            // Initialize local attributes if joining as a late client
            foreach (var attr in _networkedAttributes)
            {
                _localAttributes[attr.AttributeHash] = attr.Value;
            }
        }

        public override void OnNetworkDespawn()
        {
            _networkedAttributes.OnListChanged -= OnNetworkedAttributesChanged;
            _clientActiveTagNames.Clear();
            base.OnNetworkDespawn();
        }

        private void OnNetworkedAttributesChanged(NetworkListEvent<NetworkedAttribute> changeEvent)
        {
            if (!IsServer && changeEvent.Type == NetworkListEvent<NetworkedAttribute>.EventType.Value)
            {
                _localAttributes[changeEvent.Value.AttributeHash] = changeEvent.Value.Value;
            }
        }

        [Inject]
        public void Construct(AbilitySystemUseCase abilitySystem)
        {
            _abilitySystem = abilitySystem;
        }

        // IAbilityController Implementation
        public GameplayTagContainer ActiveTags => _activeTags;

        public bool HasTag(GameplayTag tag)
        {
            if (tag == null) return false;
            if (IsServer) return _activeTags.HasTag(tag);

            // Client-side fallback check using synchronized strings
            return _clientActiveTagNames.Contains(tag.name);
        }

        public void AddTag(GameplayTag tag)
        {
            if (IsServer)
            {
                _activeTags.AddTag(tag);
                SyncTagsClientRpc(tag.name, true);
            }
            else if (IsOwner)
            {
                // Optimistically add locally
                _clientActiveTagNames.Add(tag.name);
                RequestTagChangeServerRpc(tag.name, true);
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (IsServer)
            {
                _activeTags.RemoveTag(tag);
                SyncTagsClientRpc(tag.name, false);
            }
            else if (IsOwner)
            {
                // Optimistically remove locally
                _clientActiveTagNames.Remove(tag.name);
                RequestTagChangeServerRpc(tag.name, false);
            }
        }

        [ServerRpc]
        private void RequestTagChangeServerRpc(string tagName, bool add)
        {
            // To find the tag object on the server, we need a reference.
            // For now, we'll search for it in the ProjectLifetimeScope's known tags.
            // This is a temporary solution until we have a proper GameplayTagRegistry.
            var scope = UnityEngine.Object.FindAnyObjectByType<TinCan.Core.Infrastructure.ProjectLifetimeScope>();
            if (scope == null) return;

            // We need to find the tag. We can't easily iterate all tags on the scope without reflection
            // or if they are in a list. But we know _buildingTag is there.

            // For now, let's just handle the build mode tag specifically if it matches.
            // This is a bit hacky but works for the current requirement.

            // A better way: Use Resources.FindObjectsOfTypeAll (only in editor or if tag is in Resources)
            // Or just have a registry.

            // Let's try to find it by name in all loaded GameplayTags
            var allTags = Resources.FindObjectsOfTypeAll<GameplayTag>();
            foreach (var tag in allTags)
            {
                if (tag.name == tagName)
                {
                    if (add) AddTag(tag);
                    else RemoveTag(tag);
                    return;
                }
            }

            Debug.LogWarning($"[AbilityNetworkMediator] Server could not find tag with name: {tagName}");
        }

        public void GrantAbility(AbilityDefinition definition)
        {
            _abilitySystem.GrantAbility(this, definition);
        }

        public void RemoveAbility(AbilityDefinition definition)
        {
            _abilitySystem.RemoveAbility(this, definition);
        }

        public bool TryActivateAbility(AbilityDefinition definition, IAbilityControllerBase? target = null)
        {
            return _abilitySystem.TryActivateAbility(this, definition, target);
        }

        public T? GetAttributeSet<T>() where T : class, IAttributeSet
        {
            if (_attributeSets.TryGetValue(typeof(T), out var set))
            {
                return set as T;
            }
            return null;
        }

        public void RegisterAttributeSet(IAttributeSet set)
        {
            _attributeSets[set.GetType()] = set;
        }

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value)
        {
            if (attribute == null) { value = default; return false; }
            int hash = attribute.GetHash();

            // Always prioritize local predicted state for the owner
            if (_localAttributes.TryGetValue(hash, out value))
            {
                return true;
            }

            // Fallback to network list (for proxies or initial state)
            for (int i = 0; i < _networkedAttributes.Count; i++)
            {
                if (_networkedAttributes[i].AttributeHash == hash)
                {
                    value = _networkedAttributes[i].Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public void SetAttribute(GameplayAttribute attribute, AttributeValue value)
        {
            if (attribute == null || (!IsServer && !IsOwner)) return;

            int hash = attribute.GetHash();

            // 1. Always update local prediction
            _localAttributes[hash] = value;

            // 2. If Server, update authoritative network list
            if (IsServer)
            {
                for (int i = 0; i < _networkedAttributes.Count; i++)
                {
                    if (_networkedAttributes[i].AttributeHash == hash)
                    {
                        var item = _networkedAttributes[i];
                        item.Value = value;
                        _networkedAttributes[i] = item;
                        return;
                    }
                }
                _networkedAttributes.Add(new NetworkedAttribute { AttributeHash = hash, Value = value });
            }
        }

        public void ResetAttributesToBase()
        {
            if (!IsServer && !IsOwner) return;

            // 1. Reset local prediction
            var keys = new List<int>(_localAttributes.Keys);
            foreach (var key in keys)
            {
                var val = _localAttributes[key];
                val.CurrentValue = val.BaseValue;
                _localAttributes[key] = val;
            }

            // 2. If server, reset authoritative list
            if (IsServer)
            {
                for (int i = 0; i < _networkedAttributes.Count; i++)
                {
                    var item = _networkedAttributes[i];
                    item.Value.CurrentValue = item.Value.BaseValue;
                    _networkedAttributes[i] = item;
                }
            }
        }

        public void HandleGameplayEvent(GameplayEventData eventData)
        {
            if (IsServer)
            {
                _abilitySystem.SendGameplayEvent(eventData);
            }
        }

        [ClientRpc]
        private void SyncTagsClientRpc(string tagName, bool added)
        {
            if (IsServer) return; // Server already has authoritative state in _activeTags

            if (added)
            {
                _clientActiveTagNames.Add(tagName);
            }
            else
            {
                _clientActiveTagNames.Remove(tagName);
            }

            Debug.Log($"[AbilitySystem] Tag {tagName} {(added ? "added" : "removed")} on client.");
        }
    }
}
