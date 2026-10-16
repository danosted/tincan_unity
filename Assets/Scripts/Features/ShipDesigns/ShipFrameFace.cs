#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A face of a ship's frame: a closed loop of node ids (each consecutive pair, and the last with the first, joined by
    /// an edge) and its <see cref="Fill"/>. Faces are kept explicitly, not derived from the graph, so a face keeps its id
    /// and fill through edits.
    /// </summary>
    public sealed class ShipFrameFace
    {
        public ShipFrameFace(int id, IReadOnlyList<int> nodes, ShipFrameFill fill)
        {
            Id = id;
            Nodes = nodes;
            Fill = fill;
        }

        public int Id { get; }
        public IReadOnlyList<int> Nodes { get; }
        public ShipFrameFill Fill { get; }

        public ShipFrameFace With(IReadOnlyList<int> nodes) => new(Id, nodes, Fill);

        public ShipFrameFace With(ShipFrameFill fill) => new(Id, Nodes, fill);

        /// <summary>Whether a and b follow each other in the loop (either way round), so the edge a-b is one of its sides.</summary>
        public bool HasSide(int a, int b)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                int from = Nodes[i], to = Nodes[(i + 1) % Nodes.Count];
                if ((from == a && to == b) || (from == b && to == a)) return true;
            }

            return false;
        }

        public override string ToString() => $"face #{Id} ({string.Join("-", Nodes)}, {Fill})";
    }
}
