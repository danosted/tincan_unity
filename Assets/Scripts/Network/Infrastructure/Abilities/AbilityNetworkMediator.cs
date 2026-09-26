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
        private IGameplayTagRegistry? _tagRegistry; // Injected when the gameplay tags installer is active
        private GameplayTagContainer _activeTags = new GameplayTagContainer(null);
        private readonly HashSet<string> _clientActiveTagNames = new();
        private readonly HashSet<string> _predictedEffectTagNames = new();
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
            _predictedEffectTagNames.Clear();
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
        public void Construct(AbilitySystemUseCase abilitySystem, IObjectResolver resolver)
        {
            _abilitySystem = abilitySystem;
            // Optional: registered by GameplayTagsFeatureInstaller, which can be switched off.
            _tagRegistry = resolver.TryResolve<IGameplayTagRegistry>(out var registry) ? registry : null;
        }

        // IAbilityController Implementation
        public GameplayTagContainer ActiveTags => _activeTags;

        public bool HasTag(GameplayTag tag)
        {
            if (tag == null) return false;
            if (IsServer) return _activeTags.HasTag(tag);

            // Client-side fallback check using synchronized strings
            return _clientActiveTagNames.Contains(tag.name) || _predictedEffectTagNames.Contains(tag.name);
        }

        public void AddEffectTag(GameplayTag tag)
        {
            if (IsServer) AddTag(tag);
            else if (IsOwner) _predictedEffectTagNames.Add(tag.name);
        }

        public void RemoveEffectTag(GameplayTag tag)
        {
            if (IsServer) RemoveTag(tag);
            else if (IsOwner) _predictedEffectTagNames.Remove(tag.name);
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
            if (!TryResolveTag(tagName, out var tag))
            {
                Debug.LogWarning($"[AbilityNetworkMediator] Server could not find tag with name: {tagName}");
                return;
            }

            if (add) AddTag(tag);
            else RemoveTag(tag);
        }

        // Tags arrive by name. The registry (GameplayTagsFeatureInstaller) is the source of truth; without it, fall
        // back to scanning loaded tag assets, which only finds tags something has already loaded.
        private bool TryResolveTag(string tagName, out GameplayTag tag)
        {
            if (_tagRegistry != null) return _tagRegistry.TryGet(tagName, out tag);

            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameplayTag>())
            {
                if (candidate.name != tagName) continue;
                tag = candidate;
                return true;
            }

            tag = null!;
            return false;
        }

        public void GrantAbility(IAbilityDefinition definition)
        {
            _abilitySystem.GrantAbility(this, (AbilityDefinition)definition);
        }

        public void RemoveAbility(IAbilityDefinition definition)
        {
            _abilitySystem.RemoveAbility(this, (AbilityDefinition)definition);
        }

        public bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase? target = null)
        {
            return _abilitySystem.TryActivateAbility(this, (AbilityDefinition)definition, target);
        }

        public T? GetAttributeSet<T>() where T : class, IAttributeSet
        {
            if (_attributeSets.TryGetValue(typeof(T), out var set))
            {
                return set as T;
            }
            return null;
        }

        public bool TryGetAttributeSet<TAttributeSet>(out TAttributeSet set) where TAttributeSet : class, IAttributeSet
        {
            set = GetAttributeSet<TAttributeSet>()!;
            return set != null;
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
