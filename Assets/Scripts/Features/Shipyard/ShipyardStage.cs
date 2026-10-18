#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard's scene objects, made on Show and destroyed on Hide: a root at <see cref="ShipyardConfig.StageOrigin"/>,
    /// the preview ship (built by the same <see cref="ShipHullAssembler"/> as ships in play), a see-through floor at the
    /// working level, the ghost of the part about to be placed, the frame overlay (node markers, highlight, pending line),
    /// the panels (<see cref="ShipyardPanelView"/>) and a free-flying camera placed by the use case.
    /// </summary>
    public sealed class ShipyardStage : IShipyardStage, IDisposable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");
        /// <summary>Thumbnails are rendered this far below the stage, where nothing else is.</summary>
        private static readonly Vector3 StudioOffset = new(0f, -500f, 0f);

        private readonly ShipyardConfig _config;
        private readonly IShipPartCatalog _catalog;
        private readonly ShipDesignsConfig _designs;
        private readonly List<GameObject> _markers = new();
        private GameObject? _root;
        private Transform? _preview;
        private Transform? _floor;
        private Camera? _camera;
        private ShipHullAssembler? _assembler;
        private GameObject? _ghost;
        private ShipPartDefinition? _ghostPart;
        private MaterialPropertyBlock? _tint;
        private LineRenderer? _hoverLine;
        private LineRenderer? _pendingLine;
        private MeshFilter? _hoverFill;
        private ShipyardPanelView? _panel;
        private ShipyardThumbnails? _thumbnails;

        public ShipyardStage(ShipyardConfig config, IShipPartCatalog catalog, ShipDesignsConfig designs)
        {
            _config = config;
            _catalog = catalog;
            _designs = designs;
        }

        public bool IsShown => _root != null;

        public event Action<string>? PartPicked;
        public event Action<ShipyardMode>? ModePicked;

        public void Show()
        {
            if (IsShown) return;

            _root = new GameObject("Shipyard");
            _root.transform.position = _config.StageOrigin;
            _preview = new GameObject("Preview").transform;
            _preview.SetParent(_root.transform, false);
            _assembler = new ShipHullAssembler(_catalog, new ShipyardPreviewBuilder(), buildFunctionalParts: true, _designs);
            _tint = new MaterialPropertyBlock();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BuildFloor";
            Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(_root.transform, false);
            floor.transform.localPosition = new Vector3(0f, ShipyardUseCase.FirstFloorY - 0.01f, 0f);
            _floor = floor.transform;
            floor.transform.localScale = Vector3.one * (_config.FloorSize / 10f);
            if (_config.FloorMaterial != null) floor.GetComponent<MeshRenderer>().sharedMaterial = _config.FloorMaterial;

            var eye = new GameObject("ShipyardCamera");
            eye.transform.SetParent(_root.transform, false);
            _camera = eye.AddComponent<Camera>();
            _camera.depth = 10f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 2000f;
            _camera.fieldOfView = _config.FieldOfView;

            _hoverLine = Line("HoverLine", 0.06f);
            _hoverFill = Fill("HoverFill");
            _pendingLine = Line("PendingLine", 0.04f);

            if (_config.PanelSettings != null)
            {
                _thumbnails = new ShipyardThumbnails(_config.StageOrigin + StudioOffset);
                _panel = new ShipyardPanelView(_root.transform, _config.PanelSettings, id => _catalog.TryGet(id, out var part) ? part : null, _thumbnails);
                _panel.PartPicked += id => PartPicked?.Invoke(id);
                _panel.ModePicked += mode => ModePicked?.Invoke(mode);
            }
        }

        public void Hide()
        {
            SetLooking(false);
            _panel?.Dispose();
            _thumbnails?.Dispose();
            if (_hoverFill != null) Object.Destroy(_hoverFill.sharedMesh);
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _preview = null;
            _floor = null;
            _camera = null;
            _ghost = null;
            _ghostPart = null;
            _assembler = null;
            _hoverLine = null;
            _hoverFill = null;
            _pendingLine = null;
            _panel = null;
            _thumbnails = null;
            _markers.Clear();
        }

        public void Dispose() => Hide();

        public void ShowDesign(ShipDesign design)
        {
            if (_assembler == null || _preview == null) return;
            _assembler.Apply(design, _preview);
        }

        public ShipyardRay CursorRay(Vector2 screenPoint)
        {
            if (_camera == null || _preview == null) return new ShipyardRay(Vector3.zero, Vector3.forward);

            var ray = _camera.ScreenPointToRay(screenPoint);
            var origin = _preview.InverseTransformPoint(ray.origin);
            var direction = _preview.InverseTransformDirection(ray.direction);

            RaycastHit? nearest = null;
            foreach (var hit in Physics.RaycastAll(ray, _camera.farClipPlane, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(_preview)) continue;
                if (nearest == null || hit.distance < nearest.Value.distance) nearest = hit;
            }

            return nearest is { } found
                ? new ShipyardRay(origin, direction, true, _preview.InverseTransformPoint(found.point), _preview.InverseTransformDirection(found.normal))
                : new ShipyardRay(origin, direction);
        }

        public void ShowLevel(float floorY)
        {
            if (_floor == null) return;
            // Just under the floor, so what stands on it is drawn over the grid.
            _floor.localPosition = new Vector3(0f, floorY - 0.01f, 0f);
        }

        public void ShowGhost(ShipPartDefinition part, Vector3 position, Quaternion rotation, bool valid) =>
            ShowCopy(part, position, rotation, valid ? _config.GhostValid : _config.GhostBlocked, 1f);

        public void ShowHighlight(ShipPartDefinition part, Vector3 position, Quaternion rotation) =>
            ShowCopy(part, position, rotation, _config.GhostBlocked, _config.HighlightScale);

        public void HideGhost()
        {
            if (_ghost != null) Object.Destroy(_ghost);
            _ghost = null;
            _ghostPart = null;
        }

        public void ShowFrameMarks(ShipFrame frame, ShipyardFrameMarks marks)
        {
            if (_preview == null || _config.GhostMaterial == null) return;

            while (_markers.Count < frame.Nodes.Count) _markers.Add(Marker());
            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (i >= frame.Nodes.Count)
                {
                    marker.SetActive(false);
                    continue;
                }

                var node = frame.Nodes[i];
                marker.transform.localPosition = frame.Position(node);
                marker.SetActive(true);
                bool hovered = marks.Hover.Is(ShipyardFramePickKind.Node) && marks.Hover.Id == node.Id;
                Tint(marker.GetComponent<Renderer>(), hovered
                    ? marks.Deleting ? _config.GhostBlocked : _config.HoverColour
                    : marks.Selected == node.Id ? _config.SelectedColour : _config.NodeColour);
            }

            ShowHover(frame, marks);
            ShowHoverFill(frame, marks);
            ShowPending(frame, marks);
        }

        public void HideFrameMarks()
        {
            foreach (var marker in _markers) marker.SetActive(false);
            if (_hoverLine != null) _hoverLine.enabled = false;
            if (_hoverFill != null) _hoverFill.gameObject.SetActive(false);
            if (_pendingLine != null) _pendingLine.enabled = false;
        }

        public void PlaceCamera(ShipyardCameraPose pose)
        {
            if (_camera == null || _preview == null) return;
            _camera.transform.SetPositionAndRotation(_preview.TransformPoint(pose.Position), _preview.rotation * pose.Rotation);
        }

        public void SetLooking(bool looking)
        {
            Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !looking;
        }

        public void ShowPanel(ShipyardPanelModel model) => _panel?.Show(model);

        public bool IsPointerOverPanel(Vector2 screenPoint) => _panel?.IsPointerOver(screenPoint) ?? false;

        /// <summary>
        /// The face under the cursor, filled see-through on both sides (a little off its plane, so it shows over planks and
        /// over an open face alike).
        /// </summary>
        private void ShowHoverFill(ShipFrame frame, ShipyardFrameMarks marks)
        {
            if (_hoverFill == null) return;

            var face = marks.Hover.Is(ShipyardFramePickKind.Face)
                ? ShipFrameShape.From(frame).Faces.FirstOrDefault(f => f.Face.Id == marks.Hover.Id)
                : default;
            _hoverFill.gameObject.SetActive(face.Face != null && face.Triangles.Count > 0);
            if (face.Face == null || face.Triangles.Count == 0) return;

            // Clear of the planks: a floor's hang below its plane, a wall's are centred on it.
            const float Clear = 0.04f;
            float thickness = _designs.Frames.PlankThickness;
            var facing = face.Facing.normalized;
            var front = facing * (face.IsWall ? thickness * 0.5f + Clear : Clear);
            var back = -facing * (face.IsWall ? thickness * 0.5f + Clear : thickness + Clear);
            var vertices = face.Corners.Select(c => c + front).Concat(face.Corners.Select(c => c + back)).ToList();
            var triangles = new List<int>();
            int count = face.Corners.Count;
            for (int i = 0; i + 2 < face.Triangles.Count; i += 3)
            {
                int a = face.Triangles[i], b = face.Triangles[i + 1], c = face.Triangles[i + 2];
                triangles.AddRange(new[] { a, b, c, count + a, count + c, count + b });
            }

            var mesh = _hoverFill.sharedMesh;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var colour = marks.Deleting ? _config.GhostBlocked : _config.HoverColour;
            colour.a = 0.35f;
            Tint(_hoverFill.GetComponent<Renderer>(), colour);
        }

        private MeshFilter Fill(string name)
        {
            var fill = new GameObject(name);
            fill.transform.SetParent(_preview, false);
            var filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = new Mesh { name = name };
            fill.AddComponent<MeshRenderer>().sharedMaterial = _config.GhostMaterial;
            fill.SetActive(false);
            return filter;
        }

        private void ShowHover(ShipFrame frame, ShipyardFrameMarks marks)
        {
            if (_hoverLine == null) return;

            var points = marks.Hover.Kind switch
            {
                ShipyardFramePickKind.Edge when frame.TryGetEdge(marks.Hover.Id, out var edge) => new[] { frame.Position(edge.A), frame.Position(edge.B) },
                ShipyardFramePickKind.Face when frame.TryGetFace(marks.Hover.Id, out var face) => face.Nodes.Append(face.Nodes[0]).Select(frame.Position).ToArray(),
                _ => Array.Empty<Vector3>(),
            };
            _hoverLine.enabled = points.Length > 1;
            if (!_hoverLine.enabled) return;

            // A little above the planks, so the outline is not hidden in them.
            _hoverLine.positionCount = points.Length;
            _hoverLine.SetPositions(points.Select(p => p + Vector3.up * 0.03f).ToArray());
            Tint(_hoverLine, marks.Deleting ? _config.GhostBlocked : _config.HoverColour);
        }

        private void ShowPending(ShipFrame frame, ShipyardFrameMarks marks)
        {
            if (_pendingLine == null) return;

            _pendingLine.enabled = marks.Selected is { } selected && marks.PendingTo != null && frame.TryGetNode(selected, out _);
            if (!_pendingLine.enabled) return;

            _pendingLine.positionCount = 2;
            _pendingLine.SetPosition(0, frame.Position(marks.Selected!.Value) + Vector3.up * 0.03f);
            _pendingLine.SetPosition(1, marks.PendingTo!.Value + Vector3.up * 0.03f);
            Tint(_pendingLine, _config.SelectedColour);
        }

        private GameObject Marker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "NodeMarker";
            Object.Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(_preview, false);
            marker.transform.localScale = Vector3.one * _config.NodeMarkerSize;
            marker.GetComponent<Renderer>().sharedMaterial = _config.GhostMaterial;
            return marker;
        }

        private LineRenderer Line(string name, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(_preview, false);
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.sharedMaterial = _config.GhostMaterial;
            line.enabled = false;
            return line;
        }

        private void Tint(Renderer renderer, Color colour)
        {
            _tint!.SetColor(BaseColor, colour);
            _tint.SetColor(Color, colour);
            renderer.SetPropertyBlock(_tint);
        }

        /// <summary>The ghost: a see-through copy of the part at a pose, in a colour, a little bigger to show over a part.</summary>
        private void ShowCopy(ShipPartDefinition part, Vector3 position, Quaternion rotation, Color colour, float scale)
        {
            if (_preview == null || _config.GhostMaterial == null) return;

            if (_ghost == null || _ghostPart != part)
            {
                HideGhost();
                var prefab = part.Visual != null ? part.Visual : part.NetworkedPrefab;
                if (prefab == null) return;

                _ghost = ShipyardPrefabs.InstantiateStripped(prefab, _preview, keepColliders: false);
                _ghost.name = "Ghost";
                _ghostPart = part;
                foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = _config.GhostMaterial;
                    renderer.sharedMaterials = materials;
                }
            }

            _ghost.transform.SetLocalPositionAndRotation(position, rotation);
            _ghost.transform.localScale = Vector3.one * scale;
            _ghost.SetActive(true);

            foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>(true)) Tint(renderer, colour);
        }
    }
}
