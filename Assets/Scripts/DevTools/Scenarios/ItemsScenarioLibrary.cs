#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Abilities;
using TinCan.Features.Items;
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Scenario steps for items and equipment. Commands change what the subject holds (server only). Probes read this
    /// peer's view: the replicated held item, the visual under the player, and the abilities granted locally, which
    /// differ per peer by design (the server and the owner grant; proxies never do).
    /// </summary>
    public sealed class ItemsScenarioLibrary : IScenarioLibrary
    {
        private readonly ScenarioSubject _subject;
        private readonly INetworkService _network;
        private readonly ItemCatalog _catalog;
        private readonly AbilitySystemUseCase _abilities;

        public ItemsScenarioLibrary(ScenarioSubject subject, INetworkService network, ItemCatalog catalog, AbilitySystemUseCase abilities)
        {
            _subject = subject;
            _network = network;
            _catalog = catalog;
            _abilities = abilities;
        }

        public IEnumerable<ScenarioCommand> Commands => new[]
        {
            new ScenarioCommand("EquipSubject", Equip),
            new ScenarioCommand("UnequipSubject", _ => Unequip())
        };

        public IEnumerable<ScenarioProbe> Probes => new[]
        {
            new ScenarioProbe("SubjectHolds", Holds),
            new ScenarioProbe("SubjectHasAbility", name => CheckAbility(name, expected: true)),
            new ScenarioProbe("SubjectLacksAbility", name => CheckAbility(name, expected: false)),
            new ScenarioProbe("SubjectVisualShown", name => CheckVisual(name, expected: true)),
            new ScenarioProbe("SubjectVisualHidden", name => CheckVisual(name, expected: false))
        };

        private ScenarioCheck Equip(string itemName)
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var item = _catalog.FindByName(itemName);
            if (item == null) return ScenarioCheck.Fail($"no item named '{itemName}' in the catalog");
            var equipment = Equipment();
            if (equipment == null) return ScenarioCheck.Fail("subject has no equipment");

            if (!equipment.IsEmptyHanded) equipment.TryUnequip();
            return equipment.TryEquip(item) ? ScenarioCheck.Pass($"{itemName} equipped") : ScenarioCheck.Fail($"equip of {itemName} refused");
        }

        private ScenarioCheck Unequip()
        {
            if (!_network.IsServer) return ScenarioCheck.Fail("server-only command");
            var equipment = Equipment();
            if (equipment == null) return ScenarioCheck.Fail("subject has no equipment");

            string was = equipment.Held?.name ?? "none";
            equipment.TryUnequip();
            return ScenarioCheck.Pass($"was holding {was}");
        }

        /// <summary>Argument is an item asset name, or "none" for empty hands.</summary>
        private ScenarioCheck Holds(string itemName)
        {
            var equipment = Equipment();
            if (equipment == null) return ScenarioCheck.Fail("subject has no equipment");

            string held = equipment.Held?.name ?? "none";
            return string.Equals(held, itemName, StringComparison.Ordinal)
                ? ScenarioCheck.Pass($"holding {held}")
                : ScenarioCheck.Fail($"holding {held}");
        }

        private ScenarioCheck CheckAbility(string abilityName, bool expected)
        {
            var subject = _subject.Resolve();
            if (subject == null) return ScenarioCheck.Fail("no subject player");

            var ability = FindAbility(abilityName);
            if (ability == null) return ScenarioCheck.Fail($"no AbilityDefinition named '{abilityName}' is loaded");

            bool has = _abilities.HasAbility(subject, ability);
            string detail = $"{abilityName} {(has ? "granted" : "not granted")} on {(_network.IsServer ? "server" : "client")}";
            return has == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private ScenarioCheck CheckVisual(string visualName, bool expected)
        {
            var body = _subject.Body;
            if (body == null) return ScenarioCheck.Fail("no subject body");

            var visual = body.FindDescendant(visualName);
            if (visual == null) return ScenarioCheck.Fail($"no child named '{visualName}' on the player");

            bool shown = visual.gameObject.activeSelf;
            string detail = $"{visualName} {(shown ? "shown" : "hidden")}";
            return shown == expected ? ScenarioCheck.Pass(detail) : ScenarioCheck.Fail(detail);
        }

        private IEquipment? Equipment() => EquipmentLocator.Resolve(_subject.Resolve());

        private AbilityDefinition? FindAbility(string name) =>
            _catalog.All.SelectMany(item => item.GrantedAbilities).FirstOrDefault(ability => ability != null && ability.name == name)
            ?? Resources.FindObjectsOfTypeAll<AbilityDefinition>().FirstOrDefault(ability => ability.name == name);
    }
}
