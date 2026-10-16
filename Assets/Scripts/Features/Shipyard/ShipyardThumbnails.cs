#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Pictures of parts for the building panel: each part's stripped copy is rendered once, far below the stage, into a
    /// small texture, and kept until <see cref="Dispose"/>. A part without a prefab has no picture.
    /// </summary>
    public sealed class ShipyardThumbnails : IDisposable
    {
        private const int Size = 96;
        private static readonly Vector3 ViewDirection = new Vector3(-1f, -0.8f, 1.2f).normalized;

        private readonly Dictionary<string, RenderTexture?> _pictures = new();
        private readonly Vector3 _studio;

        public ShipyardThumbnails(Vector3 studio) => _studio = studio;

        public Texture? Of(ShipPartDefinition part)
        {
            if (_pictures.TryGetValue(part.PartId, out var picture)) return picture;

            picture = Render(part);
            _pictures[part.PartId] = picture;
            return picture;
        }

        public void Dispose()
        {
            foreach (var picture in _pictures.Values)
            {
                if (picture == null) continue;
                picture.Release();
                Object.Destroy(picture);
            }

            _pictures.Clear();
        }

        private RenderTexture? Render(ShipPartDefinition part)
        {
            var prefab = part.Visual != null ? part.Visual : part.NetworkedPrefab;
            if (prefab == null) return null;

            var holder = new GameObject("ThumbnailStudio");
            holder.transform.position = _studio;
            try
            {
                var copy = ShipyardPrefabs.InstantiateStripped(prefab, holder.transform, keepColliders: false);
                var renderers = copy.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;

                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

                var picture = new RenderTexture(Size, Size, 16) { name = $"Thumbnail {part.PartId}" };
                var camera = new GameObject("ThumbnailCamera").AddComponent<Camera>();
                camera.transform.SetParent(holder.transform, false);
                camera.fieldOfView = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.17f, 0.2f, 1f);
                float distance = Mathf.Max(1f, bounds.extents.magnitude / Mathf.Sin(15f * Mathf.Deg2Rad));
                camera.transform.position = bounds.center - ViewDirection * distance;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = distance * 3f;
                camera.targetTexture = picture;
                camera.Render();
                camera.targetTexture = null;
                return picture;
            }
            finally
            {
                Object.Destroy(holder);
            }
        }
    }
}
