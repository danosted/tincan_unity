#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Items;

namespace TinCan.Tests.Fakes
{
    /// <summary>A requester that is its own equipment (the shape handlers see when the player actor implements IEquipment).</summary>
    public class FakeEquipmentActor : IActor, IEquipment
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public ItemDefinition? Held { get; set; }
        public int Equips { get; private set; }
        public int Unequips { get; private set; }

        public bool TryEquip(ItemDefinition item)
        {
            if (Held != null) return false;
            Held = item;
            Equips++;
            return true;
        }

        public bool TryUnequip()
        {
            if (Held == null) return false;
            Held = null;
            Unequips++;
            return true;
        }
    }
}
