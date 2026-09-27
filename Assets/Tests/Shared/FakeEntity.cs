#nullable enable
using System;
using TinCan.Core.Domain.Entities;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>An entity without networking: add it to a test object's root to make that hierarchy one entity.</summary>
    public sealed class FakeEntity : MonoBehaviour, IEntity
    {
        public Guid EntityId { get; set; } = Guid.NewGuid();
        public GameObject Root => gameObject;
    }
}
