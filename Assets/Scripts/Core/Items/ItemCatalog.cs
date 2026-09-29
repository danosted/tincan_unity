#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Core.Items
{
    /// <summary>
    /// Id ↔ definition lookup for every item the game knows, so equipment state can travel as an int. Built from the
    /// items installer's list. Invalid entries (id 0 or below, duplicate ids) are rejected and reported rather than
    /// silently shadowing each other.
    /// </summary>
    public sealed class ItemCatalog
    {
        public const int None = 0;

        private readonly Dictionary<int, ItemDefinition> _byId = new();
        private readonly List<string> _problems = new();

        public ItemCatalog(IEnumerable<ItemDefinition?> items)
        {
            foreach (var item in items)
            {
                if (item == null) continue;

                switch (item.Id)
                {
                    case <= None:
                        _problems.Add($"{item.name} has id {item.Id}; ids must be positive.");
                        break;
                    case var id when _byId.TryGetValue(id, out var existing):
                        _problems.Add($"{item.name} reuses id {id} of {existing.name}; it is ignored.");
                        break;
                    default:
                        _byId.Add(item.Id, item);
                        break;
                }
            }
        }

        public IReadOnlyCollection<ItemDefinition> All => _byId.Values;

        /// <summary>Configuration errors found while building; the installer logs them.</summary>
        public IReadOnlyList<string> Problems => _problems;

        public bool TryGet(int id, out ItemDefinition item) => _byId.TryGetValue(id, out item!);

        public ItemDefinition? Find(int id) => _byId.TryGetValue(id, out var item) ? item : null;

        public bool Contains(ItemDefinition? item) => item != null && _byId.TryGetValue(item.Id, out var known) && known == item;

        public ItemDefinition? FindByName(string name) => _byId.Values.FirstOrDefault(item => item.name == name);
    }
}
