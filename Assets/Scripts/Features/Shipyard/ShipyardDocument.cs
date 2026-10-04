#nullable enable
using System.Collections.Generic;
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The design open in the shipyard and its history. Every change is a <see cref="ShipDesignEdit"/>: applying one
    /// keeps its inverse for Undo, and undoing keeps the inverse of that for Redo. A new edit clears Redo. Opening another
    /// design starts a fresh history.
    /// </summary>
    public sealed class ShipyardDocument
    {
        private readonly ShipDesignEditProcessor _edits;
        private readonly IShipPartCatalog _catalog;
        private readonly ShipDesignLimits _limits;
        private readonly Stack<ShipDesignEdit> _undo = new();
        private readonly Stack<ShipDesignEdit> _redo = new();

        public ShipyardDocument(ShipDesign design, ShipDesignEditProcessor edits, IShipPartCatalog catalog, ShipDesignLimits limits)
        {
            Design = design;
            _edits = edits;
            _catalog = catalog;
            _limits = limits;
        }

        public ShipDesign Design { get; private set; }
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        /// <summary>True when the design has changed since it was opened or last marked saved.</summary>
        public bool IsDirty { get; private set; }

        /// <summary>What the edit would do, without doing it (the ghost's colour).</summary>
        public ShipDesignEditResult Try(ShipDesignEdit edit) => _edits.Apply(Design, edit, _catalog, _limits);

        public ShipDesignEditResult Apply(ShipDesignEdit edit)
        {
            var result = Try(edit);
            if (!result.Applied) return result;

            Design = result.Design;
            _undo.Push(result.Undo!);
            _redo.Clear();
            IsDirty = true;
            return result;
        }

        public bool Undo() => Step(_undo, _redo);

        public bool Redo() => Step(_redo, _undo);

        public void Open(ShipDesign design)
        {
            Design = design;
            _undo.Clear();
            _redo.Clear();
            IsDirty = false;
        }

        public void Rename(string name)
        {
            if (name == Design.Name) return;
            Design = Design.WithName(name);
            IsDirty = true;
        }

        public void MarkSaved() => IsDirty = false;

        private bool Step(Stack<ShipDesignEdit> from, Stack<ShipDesignEdit> to)
        {
            if (from.Count == 0) return false;

            var result = Try(from.Pop());
            if (!result.Applied) return false;

            Design = result.Design;
            to.Push(result.Undo!);
            IsDirty = true;
            return true;
        }
    }
}
