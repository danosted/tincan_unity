#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Input;
using TinCan.Core.Ship.Parts;
using TinCan.Features.Shipyard;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardPanelPresenter"/>: parts grouped by category, the keys from the bindings, rows for the mode.</summary>
    public class ShipyardPanelPresenterTests
    {
        private ShipyardInputContext _controls = null!;
        private ShipPartDefinition[] _parts = null!;

        [SetUp]
        public void SetUp()
        {
            _controls = ScriptableObject.CreateInstance<ShipyardInputContext>();
            _controls.Place = InputActionId.Create("Shipyard/Place");
            _controls.Fly = InputActionId.Create("Shipyard/Fly");
            _controls.Fill = InputActionId.Create("Shipyard/Fill");
            _parts = new[]
            {
                Part("hull.block", "Hull"), Part("prop.engine", "Drive"), Part("hull.deck", "Hull"),
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var part in _parts) Object.DestroyImmediate(part);
            Object.DestroyImmediate(_controls.Place);
            Object.DestroyImmediate(_controls.Fly);
            Object.DestroyImmediate(_controls.Fill);
            Object.DestroyImmediate(_controls);
        }

        private static ShipPartDefinition Part(string id, string category)
        {
            var part = ShipPartDefinition.Create(id);
            var serialized = new UnityEditor.SerializedObject(part);
            serialized.FindProperty("_category").stringValue = category;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return part;
        }

        private ShipyardPanelModel Build(ShipyardMode mode, string? selected = null, bool deleting = false) =>
            new ShipyardPanelPresenter().Build(mode, _parts, selected, deleting, _controls, null, action => action == null ? "?" : $"[{action.Path}]");

        [Test]
        public void PartsMode_GroupsPartsByCategory_InCatalogOrder_AndMarksTheSelectedOne()
        {
            var model = Build(ShipyardMode.Parts, "hull.deck");

            Assert.That(model.Parts.Select(p => p.PartId), Is.EqualTo(new[] { "hull.block", "hull.deck", "prop.engine" }));
            Assert.That(model.Parts.Single(p => p.Selected).PartId, Is.EqualTo("hull.deck"));
        }

        [Test]
        public void TheControls_ShowTheBoundKeys_AndWhatTheyDoInTheMode()
        {
            var parts = Build(ShipyardMode.Parts);
            var frame = Build(ShipyardMode.Frame, deleting: true);

            Assert.That(parts.Controls.First().ToString(), Is.EqualTo("[Shipyard/Place]: Place the selected part"));
            Assert.That(parts.Controls.Any(r => r.Keys == "[Shipyard/Fly]" && r.Action == "Fly"), Is.True);
            Assert.That(frame.Parts, Is.Empty);
            Assert.That(frame.Controls.First().Action, Is.EqualTo("Delete the node, edge or face"));
            Assert.That(frame.Controls.Any(r => r.Keys == "[Shipyard/Fill]"), Is.True);
            Assert.That(frame.Signature, Is.Not.EqualTo(parts.Signature));
        }
    }
}
