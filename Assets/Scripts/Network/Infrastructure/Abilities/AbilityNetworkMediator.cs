#nullable enable
using Unity.Collections;
using Unity.Netcode;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using TinCan.Features.Abilities.Cues;
using System.Collections.Generic;
using System;
using VContainer;
using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Network.Infrastructure.Abilities
{
    /// <summary>
    /// NGO Mediator for the Ability System: replicates one GAS actor's tags and attributes. Tags travel as a
    /// server-written set of names, so a late joiner receives the whole set with the spawn. On a client,
    /// <see cref="HasTag"/> combines that set with the owner's pending tag requests and predicted effect tags
    /// (<see cref="ClientTagState"/>). It also carries burst cues from the server to the clients (<see cref="IGameplayCueRelay"/>).
    /// </summary>
    public class AbilityNetworkMediator : NetworkMediator, IAbilityControllerBase, IGameplayCueRelay
    {
        private AbilitySystemUseCase _abilitySystem = null!; // Injected
        private IGameplayTagRegistry? _tagRegistry; // Injected when the gameplay tags installer is active
        private IGameplayCuePlayer? _cuePlayer; // Injected when the gameplay cues installer is active
        private readonly List<ulong> _cueTargets = new();
        private GameplayTagContainer _activeTags = new GameplayTagContainer(null);
        private readonly ClientTagState _clientTags = new();
        private readonly List<string> _replicatedTagScratch = new();
        private readonly Dictionary<Type, IAttributeSet> _attributeSets = new();
        private NetworkList<NetworkedAttribute> _networkedAttributes = null!; // Initialized in Awake
        private NetworkList<FixedString64Bytes> _replicatedTags = null!; // Initialized in Awake; written by the server only

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
            _replicatedTags = new NetworkList<FixedString64Bytes>();
        }
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _networkedAttributes.OnListChanged += OnNetworkedAttributesChanged;
            _replicatedTags.OnListChanged += OnReplicatedTagsChanged;
            if (!IsServer) RefreshReplicatedTags(); // a late joiner receives the whole set with the spawn

            // Initialize local attributes if joining as a late client
            foreach (var attr in _networkedAttributes)
            {
                _localAttributes[attr.AttributeHash] = attr.Value;
            }
        }

        public override void OnNetworkDespawn()
        {
            _networkedAttributes.OnListChanged -= OnNetworkedAttributesChanged;
            _replicatedTags.OnListChanged -= OnReplicatedTagsChanged;
            _clientTags.Clear();
            base.OnNetworkDespawn();
        }

        private void OnReplicatedTagsChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
        {
            if (!IsServer) RefreshReplicatedTags();
        }

        // Few tags per actor: rebuilding the whole view is simpler than tracking each list event kind.
        private void RefreshReplicatedTags()
        {
            _replicatedTagScratch.Clear();
            foreach (var name in _replicatedTags) _replicatedTagScratch.Add(name.ToString());
            _clientTags.SetReplicated(_replicatedTagScratch);
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
            _cuePlayer = resolver.TryResolve<IGameplayCuePlayer>(out var cuePlayer) ? cuePlayer : null;
        }

        // IAbilityController Implementation
        public GameplayTagContainer ActiveTags => _activeTags;

        public bool HasTag(GameplayTag tag)
        {
            if (tag == null) return false;
            if (IsServer) return _activeTags.HasTag(tag);

            // Same rule as the server (parent tags match); held names resolve through the tag registry.
            return _clientTags.Has(tag, _tagRegistry);
        }

        public void AddEffectTag(GameplayTag tag)
        {
            if (IsServer) AddTag(tag);
            else if (IsOwner) _clientTags.AddPredicted(tag.name);
        }

        public void RemoveEffectTag(GameplayTag tag)
        {
            if (IsServer) RemoveTag(tag);
            else if (IsOwner) _clientTags.RemovePredicted(tag.name);
        }

        public void AddTag(GameplayTag tag)
        {
            if (IsServer)
            {
                _activeTags.AddTag(tag);
                var name = ToWireName(tag);
                if (!_replicatedTags.Contains(name)) _replicatedTags.Add(name);
            }
            else if (IsOwner)
            {
                _clientTags.AddOptimistic(tag.name);
                RequestTagChangeServerRpc(tag.name, true);
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (IsServer)
            {
                _activeTags.RemoveTag(tag);
                _replicatedTags.Remove(ToWireName(tag));
            }
            else if (IsOwner)
            {
                _clientTags.RemoveOptimistic(tag.name);
                RequestTagChangeServerRpc(tag.name, false);
            }
        }

        private FixedString64Bytes ToWireName(GameplayTag tag)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(tag.name) > FixedString64Bytes.UTF8MaxLengthInBytes)
            {
                Debug.LogError($"[AbilityNetworkMediator] Tag name '{tag.name}' is longer than {FixedString64Bytes.UTF8MaxLengthInBytes} bytes and cannot replicate intact.", this);
            }
            var name = new FixedString64Bytes();
            name.CopyFromTruncated(tag.name);
            return name;
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

        public bool IsOwnedLocally => IsSpawned && IsOwner;

        public void RelayCue(GameplayTag cue, bool excludeOwner)
        {
            if (!IsServer || !IsSpawned || cue == null) return;

            // The host already played it locally; the owner may have predicted it.
            _cueTargets.Clear();
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId || (excludeOwner && clientId == OwnerClientId)) continue;
                _cueTargets.Add(clientId);
            }
            if (_cueTargets.Count == 0) return;

            ExecuteCueClientRpc(cue.name, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = _cueTargets } });
        }

        // Unreliable: a burst is presentation, and a late or lost one must not stall anything.
        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void ExecuteCueClientRpc(string cueTag, ClientRpcParams rpcParams = default)
        {
            if (IsServer || _cuePlayer == null) return;
            if (TryResolveTag(cueTag, out var cue)) _cuePlayer.Play(cue, this);
        }
    }
}
