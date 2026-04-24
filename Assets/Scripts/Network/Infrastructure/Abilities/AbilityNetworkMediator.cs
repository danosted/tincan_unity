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
    public class AbilityNetworkMediator : NetworkMediator, IAbilityController
    {
        private AbilitySystemUseCase _abilitySystem = null!; // Injected
        private GameplayTagContainer _activeTags = new GameplayTagContainer(null);
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

        public bool HasTag(GameplayTag tag) => _activeTags.HasTag(tag);

        public void AddTag(GameplayTag tag)
        {
            if (!IsServer) return;
            _activeTags.AddTag(tag);
            SyncTagsClientRpc(tag.name, true);
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!IsServer) return;
            _activeTags.RemoveTag(tag);
            SyncTagsClientRpc(tag.name, false);
        }

        public void GrantAbility(AbilityDefinition definition)
        {
            _abilitySystem.GrantAbility(this, definition);
        }

        public void RemoveAbility(AbilityDefinition definition)
        {
            // Future
        }

        public bool TryActivateAbility(AbilityDefinition definition)
        {
            // Note: In the Input-Driven Simulation paradigm, activation intent should be
            // part of the InputState synchronized via the movement/action mediator.
            // This method returns whether the actor HAS the ability to be activated.
            return HasTag(definition.AbilityTag);
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
            if (IsServer) return;
            Debug.Log($"[AbilitySystem] Tag {tagName} {(added ? "added" : "removed")} on client.");
        }
    }
}
