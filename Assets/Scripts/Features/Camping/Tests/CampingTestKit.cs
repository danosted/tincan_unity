#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Events;
using TinCan.Core.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.Camping.Tests
{
    /// <summary>The config and items the handler tests share; Dispose destroys them.</summary>
    internal sealed class CampingTestKit : IDisposable
    {
        public CampingTestKit()
        {
            Firewood = ItemDefinition.Create(5, "ITEM_Firewood");
            Tent = ItemDefinition.Create(6, "ITEM_TentRoll");
            Other = ItemDefinition.Create(3, "ITEM_RepairTool");
            Config = ScriptableObject.CreateInstance<CampingConfig>();
            Config.FirewoodItem = Firewood;
            Config.TentItem = Tent;
            Config.BurnSecondsPerLog = 180f;
            Config.MaxBurnSeconds = 600f;
        }

        public ItemDefinition Firewood { get; }
        public ItemDefinition Tent { get; }
        public ItemDefinition Other { get; }
        public CampingConfig Config { get; }
        public RecordingPublisher Events { get; } = new();

        public void Dispose()
        {
            Object.DestroyImmediate(Firewood);
            Object.DestroyImmediate(Tent);
            Object.DestroyImmediate(Other);
            Object.DestroyImmediate(Config);
        }

        internal sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }
    }
}
