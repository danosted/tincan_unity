#nullable enable
using System;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard's scene objects, made on Show and destroyed on Hide: a root at <see cref="ShipyardConfig.StageOrigin"/>,
    /// the preview ship (built by the same <see cref="ShipHullAssembler"/> as ships in play), a see-through floor at the working level, the ghost of
    /// the part about to be placed, and an orbit camera around the ship.
    /// </summary>
    public sealed class ShipyardStage : IShipyardStage, IDisposable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Color = Shader.PropertyToID("_Color");

        private readonly ShipyardConfig _config;
        private readonly IShipPartCatalog _catalog;
        private GameObject? _root;
        private Transform? _preview;
        private Transform? _floor;
        private Camera? _camera;
        private ShipHullAssembler? _assembler;
        private GameObject? _ghost;
        private ShipPartDefinition? _ghostPart;
        private MaterialPropertyBlock? _tint;
        private Vector3 _target;
        private float _yaw = 35f;
        private float _pitch = 30f;
        private float _distance;

        public ShipyardStage(ShipyardConfig config, IShipPartCatalog catalog)
        {
            _config = config;
            _catalog = catalog;
            _distance = config.StartDistance;
        }

        public bool IsShown => _root != null;

        public void Show()
        {
            if (IsShown) return;

            _root = new GameObject("Shipyard");
            _root.transform.position = _config.StageOrigin;
            _preview = new GameObject("Preview").transform;
            _preview.SetParent(_root.transform, false);
            _assembler = new ShipHullAssembler(_catalog, new ShipyardPreviewBuilder(), buildFunctionalParts: true);
            _tint = new MaterialPropertyBlock();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BuildFloor";
            Object.Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(_root.transform, false);
            floor.transform.localPosition = new Vector3(0f, ShipyardAimProcessor.FloorY(0) - 0.01f, 0f);
            _floor = floor.transform;
            floor.transform.localScale = Vector3.one * (_config.FloorSize / 10f);
            if (_config.FloorMaterial != null) floor.GetComponent<MeshRenderer>().sharedMaterial = _config.FloorMaterial;

            var eye = new GameObject("ShipyardCamera");
            eye.transform.SetParent(_root.transform, false);
            _camera = eye.AddComponent<Camera>();
            _camera.depth = 10f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 2000f;
            PlaceCamera();
        }

        public void Hide()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _preview = null;
            _floor = null;
            _camera = null;
            _ghost = null;
            _ghostPart = null;
            _assembler = null;
        }

        public void Dispose() => Hide();

        public void ShowDesign(ShipDesign design)
        {
            if (_assembler == null || _preview == null) return;

            _assembler.Apply(design, _preview);
            _target = ShipPartPose.TryGetBounds(design, _catalog, out var bounds) ? bounds.center : Vector3.zero;
            PlaceCamera();
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

        public void ShowLevel(int level)
        {
            if (_floor == null) return;
            // Just under the level's floor, so parts standing on it are drawn over the grid.
            _floor.localPosition = new Vector3(0f, ShipyardAimProcessor.FloorY(level) - 0.01f, 0f);
        }

        public void ShowGhost(ShipPartDefinition part, ShipGridCell cell, byte orientation, bool valid) =>
            ShowCopy(part, cell, orientation, valid ? _config.GhostValid : _config.GhostBlocked, 1f);

        public void ShowHighlight(ShipPartDefinition part, ShipPartPlacement placement) =>
            ShowCopy(part, placement.Cell, placement.Orientation, _config.GhostBlocked, _config.HighlightScale);

        /// <summary>The ghost: a see-through copy of the part at a cell, in a colour, a little bigger to show over a part.</summary>
        private void ShowCopy(ShipPartDefinition part, ShipGridCell cell, byte orientation, Color colour, float scale)
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

            var placement = new ShipPartPlacement(0, part.PartId, cell, orientation);
            _ghost.transform.SetLocalPositionAndRotation(ShipPartPose.LocalPosition(placement, part), ShipPartPose.LocalRotation(placement));
            _ghost.transform.localScale = Vector3.one * scale;
            _ghost.SetActive(true);

            _tint!.SetColor(BaseColor, colour);
            _tint.SetColor(Color, colour);
            foreach (var renderer in _ghost.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(_tint);
        }

        public void HideGhost()
        {
            if (_ghost != null) Object.Destroy(_ghost);
            _ghost = null;
            _ghostPart = null;
        }

        public void Orbit(Vector2 degrees)
        {
            _yaw += degrees.x;
            _pitch = Mathf.Clamp(_pitch - degrees.y, 5f, _config.MaxPitch);
            PlaceCamera();
        }

        public void Zoom(float metres)
        {
            _distance = Mathf.Clamp(_distance - metres, _config.DistanceRange.x, _config.DistanceRange.y);
            PlaceCamera();
        }

        private void PlaceCamera()
        {
            if (_camera == null || _preview == null) return;

            var rotation = _preview.rotation * Quaternion.Euler(_pitch, _yaw, 0f);
            var target = _preview.TransformPoint(_target);
            _camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * _distance, rotation);
        }
    }
}
