#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Core.Domain.Abilities.Tags
{
    /// <summary>
    /// A container for managing multiple GameplayTags.
    /// Optimized for querying presence of tags including hierarchical checks.
    /// </summary>
    [Serializable]
    public struct GameplayTagContainer : INetworkSerializable
    {
        // For serialization in NGO, we might need a different approach if we want to sync the tags.
        // For now, let's stick to the domain logic and handle networking in the mediator.
        // Note: ScriptableObjects aren't directly serializable over the network easily without an ID mapping.

        private List<GameplayTag> _tags;

        public GameplayTagContainer(IEnumerable<GameplayTag>? tags)
        {
            _tags = tags?.ToList() ?? new List<GameplayTag>();
        }

        public bool HasTag(GameplayTag tag)
        {
            if (_tags == null || tag == null) return false;
            return _tags.Any(t => t.IsChildOf(tag));
        }

        public bool HasAny(GameplayTagContainer other)
        {
            if (_tags == null || other._tags == null) return false;
            return other._tags.Any(HasTag);
        }

        public bool HasAll(GameplayTagContainer other)
        {
            if (_tags == null || other._tags == null) return false;
            return other._tags.All(HasTag);
        }

        public void AddTag(GameplayTag tag)
        {
            _tags ??= new List<GameplayTag>();
            if (!_tags.Contains(tag))
            {
                _tags.Add(tag);
            }
        }

        public void RemoveTag(GameplayTag tag)
        {
            _tags?.Remove(tag);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Placeholder for network sync.
            // Usually involves syncing a list of Tag IDs or Names.
            // Since this is a domain struct, we might keep it clean and handle sync in the Mediator.
        }
    }
}
