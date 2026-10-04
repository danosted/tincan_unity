#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// Infrastructure: islands as GameObjects under a <c>SkyIslands</c> root in the active scene, each marked with a
    /// <see cref="SkyIslandBody"/>, solid through convex mesh colliders (the low-detail shape cut into pieces,
    /// <see cref="SkyIslandMeshProcessor.BuildPieces"/>) and, on peers that render, drawn with the detailed mesh and the
    /// island material. Removed islands go back to a pool with their meshes, so streaming does not churn objects. Peers
    /// without graphics (batch mode: the dedicated server, bots) build collision only.
    /// </summary>
    public sealed class SkyIslandBuilder : ISkyIslandBuilder
    {
        private readonly SkyIslandConfig _config;
        private readonly SkyIslandMeshProcessor _meshes;
        private readonly SkyIslandMeshData _data = new();
        private readonly SkyIslandCollisionPieces _pieces = new();
        private readonly List<Vector3> _piecePoints = new();
        private readonly List<int> _pieceTriangles = new();
        private readonly Stack<Built> _pool = new();
        private readonly HashSet<Built> _standing = new();
        private readonly bool _renders;
        private Transform? _root;

        public SkyIslandBuilder(SkyIslandConfig config, SkyIslandMeshProcessor meshes)
        {
            _config = config;
            _meshes = meshes;
            _renders = !Application.isBatchMode;
            if (_renders && config.Material == null)
            {
                Debug.LogWarning($"[SkyIslands] {config.name} has no material; islands are invisible (collision only).", config);
                _renders = false;
            }
        }

        /// <summary>The name of an island's object: unique per layout, so a lookup never finds a pooled one from an old seed.</summary>
        public static string NameOf(SkyIslandId id) => $"SkyIsland {id.Seed}:{id}";

        public object Build(in SkyIslandSpec island)
        {
            var built = _pool.Count > 0 ? _pool.Pop() : Create();
            if (built.GameObject == null) built = Create();

            var go = built.GameObject;
            go.name = NameOf(island.Id);
            built.Body.Id = island.Id;
            go.transform.SetParent(Root(), false);
            go.transform.SetPositionAndRotation(island.Top, Quaternion.identity);
            // Active before the colliders get their meshes: a reused collider given its mesh while inactive came back
            // with none (not solid).
            go.SetActive(true);
            built.Bounds = BoundsOf(island);
            _standing.Add(built);

            _meshes.BuildPieces(island, island.IsSatellite ? _config.SatelliteCollision : _config.MainCollision,
                _config.CollisionWedgeSegments, _data, _pieces);
            built.Fit(_pieces.Count);
            for (int piece = 0; piece < _pieces.Count; piece++) FillPiece(built.Pieces[piece], piece);

            if (built.Visual != null)
            {
                _meshes.Build(island, island.IsSatellite ? _config.SatelliteVisual : _config.MainVisual, _data);
                Fill(built.Visual);
            }

            return built;
        }

        public void Remove(object handle)
        {
            if (handle is not Built built || !_standing.Remove(built) || built.GameObject == null) return;
            built.GameObject.SetActive(false);
            _pool.Push(built);
        }

        public bool AnyNear(Vector3 point, float reach)
        {
            foreach (var built in _standing)
            {
                if (built.Bounds.SqrDistance(point) <= reach * reach) return true;
            }
            return false;
        }

        public void Clear()
        {
            foreach (var built in _standing) built.Discard();
            _standing.Clear();
            while (_pool.Count > 0) _pool.Pop().Discard();
            if (_root != null) Discard(_root.gameObject);
            _root = null;
        }

        /// <summary>The box the island's rock fits in: its radius around, from a little over the top down to the apex.</summary>
        private static Bounds BoundsOf(in SkyIslandSpec island)
        {
            float above = island.Radius * 0.2f;
            var size = new Vector3(island.Radius * 2f, island.Depth + above, island.Radius * 2f);
            return new Bounds(island.Top + Vector3.up * ((above - island.Depth) * 0.5f), size);
        }

        private Built Create()
        {
            var go = new GameObject("SkyIsland");
            go.SetActive(false);
            go.transform.SetParent(Root(), false);
            var body = go.AddComponent<SkyIslandBody>();

            Mesh? visual = null;
            if (_renders)
            {
                visual = new Mesh { name = "SkyIsland" };
                go.AddComponent<MeshFilter>().sharedMesh = visual;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _config.Material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            return new Built(go, body, visual);
        }

        /// <summary>One convex piece: its points, fanned into triangles (the hull is cooked from the points).</summary>
        private void FillPiece(Piece piece, int index)
        {
            _piecePoints.Clear();
            int start = _pieces.Starts[index], length = _pieces.Length(index);
            for (int i = 0; i < length; i++) _piecePoints.Add(_pieces.Points[start + i]);

            _pieceTriangles.Clear();
            for (int i = 1; i + 1 < length; i++)
            {
                _pieceTriangles.Add(0);
                _pieceTriangles.Add(i);
                _pieceTriangles.Add(i + 1);
            }

            piece.Collider.sharedMesh = null;
            piece.Mesh.Clear();
            piece.Mesh.SetVertices(_piecePoints);
            piece.Mesh.SetTriangles(_pieceTriangles, 0);
            piece.Mesh.RecalculateBounds();
            piece.Collider.sharedMesh = piece.Mesh;
            piece.Collider.enabled = true;
        }

        private void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_data.Vertices);
            mesh.SetNormals(_data.Normals);
            mesh.SetColors(_data.Colors);
            mesh.SetTriangles(_data.Triangles, 0);
            mesh.RecalculateBounds();
        }

        private Transform Root()
        {
            if (_root != null) return _root;
            _root = new GameObject("SkyIslands").transform;
            return _root;
        }

        /// <summary>Destroy, or DestroyImmediate outside play mode (edit-mode tests).</summary>
        private static void Discard(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private sealed class Piece
        {
            public readonly MeshCollider Collider;
            public readonly Mesh Mesh;

            public Piece(MeshCollider collider, Mesh mesh)
            {
                Collider = collider;
                Mesh = mesh;
            }
        }

        private sealed class Built
        {
            public readonly GameObject GameObject;
            public readonly SkyIslandBody Body;
            public readonly Mesh? Visual;
            public readonly List<Piece> Pieces = new();
            public Bounds Bounds;

            public Built(GameObject gameObject, SkyIslandBody body, Mesh? visual)
            {
                GameObject = gameObject;
                Body = body;
                Visual = visual;
            }

            /// <summary>Exactly <paramref name="count"/> pieces switched on: more colliders when needed, spares off.</summary>
            public void Fit(int count)
            {
                while (Pieces.Count < count)
                {
                    var collider = GameObject.AddComponent<MeshCollider>();
                    collider.convex = true;
                    Pieces.Add(new Piece(collider, new Mesh { name = "SkyIsland piece" }));
                }
                for (int i = count; i < Pieces.Count; i++)
                {
                    Pieces[i].Collider.sharedMesh = null;
                    Pieces[i].Collider.enabled = false;
                }
            }

            public void Discard()
            {
                foreach (var piece in Pieces) SkyIslandBuilder.Discard(piece.Mesh);
                if (Visual != null) SkyIslandBuilder.Discard(Visual);
                if (GameObject != null) SkyIslandBuilder.Discard(GameObject);
            }
        }
    }
}
