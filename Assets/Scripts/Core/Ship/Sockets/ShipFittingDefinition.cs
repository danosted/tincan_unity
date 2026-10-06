#nullable enable
using System.Text.RegularExpressions;
using UnityEngine;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>
    /// Something that can be mounted in a ship's socket: a networked prefab with its pivot at its base, placed on the
    /// socket's pose. Features contribute fittings through <c>FeatureInstaller.IExtension&lt;ShipFittingDefinition&gt;</c>
    /// (the cannon station comes from the Cannon feature). Any fitting fits any socket for now.
    /// </summary>
    [CreateAssetMenu(fileName = "FITTING_New", menuName = "TinCan/Ship/Fitting Definition")]
    public class ShipFittingDefinition : ScriptableObject
    {
        public static readonly Regex FittingIdFormat = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)*$");

        [Tooltip("Stable id, lower-case words separated by dots (weapon.cannon). It travels in the mount request.")]
        [SerializeField] private string _fittingId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [Tooltip("The networked prefab mounted on the socket (a NetworkObject with an EntityNetworkMediator on its root).")]
        [SerializeField] private GameObject? _prefab;

        public string FittingId => _fittingId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public GameObject? Prefab => _prefab;

        public static bool IsValidFittingId(string? id) => !string.IsNullOrEmpty(id) && id!.Length <= 60 && FittingIdFormat.IsMatch(id);

        /// <summary>Test and tooling seam.</summary>
        public static ShipFittingDefinition Create(string fittingId, GameObject? prefab, string displayName = "")
        {
            var fitting = CreateInstance<ShipFittingDefinition>();
            fitting.name = fittingId;
            fitting._fittingId = fittingId;
            fitting._prefab = prefab;
            fitting._displayName = displayName;
            return fitting;
        }
    }
}
