#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// Every peer with a view: draws the cloud sea around the local camera, snapped to the grid step so it never swims:
    /// the surface on a wide grid, and <see cref="CloudSeaConfig.Shells"/> see-through layers above it on a smaller one,
    /// lowest first. Each frame it hands the sea shader the shared time, the swell and the holes
    /// (<see cref="CloudSeaShaderGlobals"/>); the shader lifts and cuts the grid with the formula the game uses
    /// (<see cref="CloudSeaFormula"/>). The layers take the surface material's values each frame, so tuning it in Play
    /// shows on all of them. Presentation only; nothing here touches the simulation.
    /// </summary>
    public sealed class CloudSeaPresenter : ITickable, IDisposable
    {
        private static readonly int ShellLayer = Shader.PropertyToID("_ShellLayer");
        private static readonly int ShellCount = Shader.PropertyToID("_ShellCount");
        private static readonly int ShellSpacing = Shader.PropertyToID("_ShellSpacing");
        private static readonly int ShellHalf = Shader.PropertyToID("_ShellHalf");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        private readonly ILocalViewCamera _view;
        private readonly ICloudHoles _holes;
        private readonly ITimeService _time;
        private readonly CloudBoundaryConfig _boundary;
        private readonly CloudSeaConfig _config;
        private readonly Material? _material;
        private readonly List<Object> _owned = new();
        private readonly List<Material> _shellMaterials = new();
        private Transform? _root;
        private MeshFilter? _surface;
        private float _surfaceSize;

        public CloudSeaPresenter(ILocalViewCamera view, ICloudHoles holes, ITimeService time,
            CloudBoundaryConfig boundary, CloudSeaConfig config, Material? material)
        {
            _view = view;
            _holes = holes;
            _time = time;
            _boundary = boundary;
            _config = config;
            _material = material;
        }

        public void Tick()
        {
            Camera? camera = _view.Camera;
            if (_material == null || camera == null) return;

            _root ??= Create(_material);
            // The surface's grid reaches the far plane, however far the player's view distance sets it.
            float size = SurfaceGridSize(_config.GridSize, camera.farClipPlane);
            if (size != _surfaceSize) Resize(size);
            Vector3 eye = camera.transform.position;
            _root.position = new Vector3(Snap(eye.x, _config.GridStep), 0f, Snap(eye.z, _config.GridStep));
            for (int i = 0; i < _shellMaterials.Count; i++) MatchShell(_material, _shellMaterials[i], i + 1);
            // The surface fades out by real distance before the grid's edge and before the camera's flat far plane,
            // so turning the camera never cuts it.
            float fadeOut = FadeOut(_surfaceSize * 0.5f, camera.farClipPlane);
            CloudSeaShaderGlobals.Apply(_time.Time, _boundary.BaseAltitude, _boundary.Swell, _holes.ShapesNow,
                _config.Web(_boundary.WorldSeed), fadeOut);
            CloudSeaShaderGlobals.ApplyLook(_material);
        }

        public void Dispose()
        {
            CloudSeaShaderGlobals.Clear();
            foreach (Object owned in _owned)
            {
                if (owned != null) Object.Destroy(owned);
            }
        }

        /// <summary>Where the drawn surface has faded out: the grid's reach, but always inside the camera's flat far
        /// plane, so turning the camera never cuts it (the shader fades by real distance).</summary>
        public static float FadeOut(float reach, float farClipPlane) => Mathf.Min(reach, farClipPlane * 0.95f);

        /// <summary>The grid's centre for a camera at <paramref name="value"/>: the nearest whole step.</summary>
        public static float Snap(float value, float step) => Mathf.Round(value / step) * step;

        /// <summary>The surface grid's width: the configured one, or wider when the far plane lies past its edge.</summary>
        public static float SurfaceGridSize(float gridSize, float farClipPlane) => Mathf.Max(gridSize, farClipPlane * 2f);

        // A new surface grid of this width; the old one is destroyed (only when the view distance changes).
        private void Resize(float size)
        {
            if (_surface == null) return;
            Mesh old = _surface.sharedMesh;
            _surface.sharedMesh = Grid(size);
            _surfaceSize = size;
            if (old == null) return;
            _owned.Remove(old);
            Object.Destroy(old);
        }

        private Transform Create(Material material)
        {
            var root = new GameObject("Cloud Sea");
            _owned.Add(root);
            _surfaceSize = _config.GridSize;
            _surface = Draw(root.transform, "Surface", Grid(_surfaceSize), material);
            if (_config.Shells <= 0) return root.transform;

            Mesh shellGrid = Grid(_config.ShellGridSize);
            for (int layer = 1; layer <= _config.Shells; layer++)
            {
                var shell = new Material(material) { name = $"{material.name} (shell {layer})" };
                _owned.Add(shell);
                _shellMaterials.Add(shell);
                MatchShell(material, shell, layer);
                Draw(root.transform, $"Shell {layer}", shellGrid, shell);
            }

            return root.transform;
        }

        // A layer takes the surface's values and draws over it without writing depth; its queue puts it after the
        // layers below it.
        private void MatchShell(Material surface, Material shell, int layer)
        {
            shell.CopyPropertiesFromMaterial(surface);
            shell.SetFloat(ShellLayer, layer);
            shell.SetFloat(ZWrite, 0f);
            shell.SetFloat(ShellCount, _config.Shells);
            shell.SetFloat(ShellSpacing, _config.ShellSpacing);
            shell.SetFloat(ShellHalf, _config.ShellGridSize * 0.5f);
            shell.renderQueue = surface.renderQueue + layer;
        }

        private static MeshFilter Draw(Transform root, string name, Mesh mesh, Material material)
        {
            var layer = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            layer.transform.SetParent(root, false);
            var filter = layer.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return filter;
        }

        private Mesh Grid(float size)
        {
            Mesh mesh = BuildGrid(size, _config.GridStep);
            _owned.Add(mesh);
            return mesh;
        }

        private static Mesh BuildGrid(float size, float step)
        {
            int cells = Mathf.Max(1, Mathf.RoundToInt(size / step));
            int row = cells + 1;
            float half = cells * step * 0.5f;
            var vertices = new Vector3[row * row];
            for (int z = 0; z < row; z++)
            for (int x = 0; x < row; x++)
                vertices[z * row + x] = new Vector3(x * step - half, 0f, z * step - half);

            var indices = new int[cells * cells * 6];
            int next = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int corner = z * row + x;
                indices[next++] = corner;
                indices[next++] = corner + row;
                indices[next++] = corner + 1;
                indices[next++] = corner + 1;
                indices[next++] = corner + row;
                indices[next++] = corner + row + 1;
            }

            var mesh = new Mesh { name = "Cloud Sea Grid", indexFormat = IndexFormat.UInt32, vertices = vertices, triangles = indices };
            // The shader lifts and sinks the grid, so the bounds cover the swell, the heaps, the layers and the holes.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(half * 2f, 2000f, half * 2f));
            return mesh;
        }
    }
}
