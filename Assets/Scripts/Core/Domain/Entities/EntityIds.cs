#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TinCan.Core.Domain.Entities
{
    /// <summary>
    /// How actor ids follow from entity ids. An actor on the entity's root object has the entity's id; an actor on a
    /// child object has an id derived from the entity id and the child's path under the root (a name-based UUID), so
    /// every peer computes the same id without sending it. Actors on one object share one id.
    /// </summary>
    public static class EntityIds
    {
        /// <summary>A new random id. The only place ids are made: entities assign them, nothing else.</summary>
        public static Guid New() => Guid.NewGuid();

        /// <summary>The id of the actor at <paramref name="path"/> under an entity (an empty path is the root).</summary>
        public static Guid Derive(Guid entityId, string path)
        {
            if (string.IsNullOrEmpty(path)) return entityId;

            var pathBytes = Encoding.UTF8.GetBytes(path);
            var input = new byte[16 + pathBytes.Length];
            entityId.ToByteArray().CopyTo(input, 0);
            pathBytes.CopyTo(input, 16);

            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(input);
            var bytes = new byte[16];
            Array.Copy(hash, bytes, 16);
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5 (name-based), in Guid's little-endian layout
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
            return new Guid(bytes);
        }

        /// <summary>The path of <paramref name="child"/> under <paramref name="root"/>: names joined by '/', "" for the root.</summary>
        public static string PathUnder(Transform root, Transform child)
        {
            if (child == root) return string.Empty;
            var path = child.name;
            for (var current = child.parent; current != null && current != root; current = current.parent)
            {
                path = current.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// The id of the actor component <paramref name="actor"/>, from the entity above it. <see cref="Guid.Empty"/>
        /// while that entity has no id yet (a client before spawn). Without any entity (a local-only object or a test),
        /// null: the caller keeps a local id of its own.
        /// </summary>
        public static Guid? ActorIdFor(Component actor)
        {
            var entity = actor.GetComponentInParent<IEntity>(true);
            if (entity == null) return null;
            var entityId = entity.EntityId;
            if (entityId == Guid.Empty) return Guid.Empty;
            return Derive(entityId, PathUnder(entity.Root.transform, actor.transform));
        }
    }
}
