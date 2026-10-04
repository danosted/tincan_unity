#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TinCan.Core.Ship.Parts
{
    /// <summary>
    /// A part a ship can be built from: what it is called in a saved design, the grid cells it fills, and what is built
    /// for it. Features contribute parts through <c>FeatureInstaller.IExtension&lt;ShipPartDefinition&gt;</c>, so a part
    /// exists only where the feature that makes it work is loaded (the helm part comes from the Helm feature).
    /// Plan: .docs/plans/modular-airship-builder.md.
    /// </summary>
    [CreateAssetMenu(fileName = "PART_New", menuName = "TinCan/Ship/Part Definition")]
    public class ShipPartDefinition : ScriptableObject
    {
        /// <summary>Lower-case words separated by dots: <c>hull.block</c>, <c>core.helm</c>.</summary>
        public static readonly Regex PartIdFormat = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)*$");
        public const int MaxPartIdLength = 64;

        [Tooltip("The part's name in saved designs, unique across all parts. Never change it once designs use it: "
                 + "a design that names an unknown part keeps it but cannot build it.")]
        [SerializeField] private string _partId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [Tooltip("Groups parts in the shipyard's list.")]
        [SerializeField] private string _category = "Hull";
        [Tooltip("The cells the part fills, relative to its origin cell, before rotation. Empty means the origin cell alone.")]
        [SerializeField] private List<Vector3Int> _footprint = new();
        [Tooltip("The ship's core part: every ship has exactly one, and every other part must connect to it.")]
        [SerializeField] private bool _isCore;
        [Tooltip("Built on every peer as a plain child of the ship (meshes and colliders, no NetworkObject).")]
        [SerializeField] private GameObject? _visual;
        [Tooltip("Optional: a networked prefab the server spawns onto the ship at the part's pose (the helm).")]
        [SerializeField] private GameObject? _networkedPrefab;
        [Tooltip("Where the prefabs' pivot sits relative to the origin cell's centre, in metres. A prefab with its pivot "
                 + "at its base, standing on the floor of its cell, uses (0, -0.5, 0).")]
        [SerializeField] private Vector3 _pivotOffset;
        [Tooltip("Mass, lift, thrust and hull this part adds to the ship.")]
        [SerializeField] private ShipPartStats _stats;

        private static readonly Vector3Int[] OriginOnly = { Vector3Int.zero };

        public string PartId => _partId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Category => _category;
        public IReadOnlyList<Vector3Int> Footprint => _footprint.Count > 0 ? _footprint : OriginOnly;
        public bool IsCore => _isCore;
        public GameObject? Visual => _visual;
        public GameObject? NetworkedPrefab => _networkedPrefab;
        public Vector3 PivotOffset => _pivotOffset;
        public ShipPartStats Stats => _stats;

        public static bool IsValidPartId(string? partId) =>
            !string.IsNullOrEmpty(partId) && partId!.Length <= MaxPartIdLength && PartIdFormat.IsMatch(partId);

        /// <summary>Test and tooling seam; assets set these in the Inspector.</summary>
        public static ShipPartDefinition Create(string partId, bool isCore = false, params Vector3Int[] footprint) =>
            Create(partId, isCore, null, null, Vector3.zero, footprint);

        /// <summary>Test and tooling seam, with the prefabs the part builds.</summary>
        public static ShipPartDefinition Create(string partId, bool isCore, GameObject? visual, GameObject? networkedPrefab,
            Vector3 pivotOffset, params Vector3Int[] footprint) =>
            Create(partId, isCore, visual, networkedPrefab, pivotOffset, new ShipPartStats(), footprint);

        /// <summary>Test and tooling seam, with the prefabs the part builds and what it adds to the ship.</summary>
        public static ShipPartDefinition Create(string partId, bool isCore, GameObject? visual, GameObject? networkedPrefab,
            Vector3 pivotOffset, ShipPartStats stats, params Vector3Int[] footprint)
        {
            var part = CreateInstance<ShipPartDefinition>();
            part.name = partId;
            part._partId = partId;
            part._isCore = isCore;
            part._visual = visual;
            part._networkedPrefab = networkedPrefab;
            part._pivotOffset = pivotOffset;
            part._stats = stats;
            part._footprint = new List<Vector3Int>(footprint);
            return part;
        }
    }
}
