#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TinCan.Features.ShipDesigns;
using UnityEngine;
using VContainer;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Loadouts as <c>&lt;design key&gt;.fittings.json</c> files in the designs folder (<c>persistentDataPath/Ships</c>),
    /// beside each <c>&lt;design key&gt;.ship.json</c>, plus the built-in ones listed in <see cref="ShipSocketsConfig"/>
    /// (TextAssets named after their design). Saves write a temporary file and swap it in, like the design store.
    /// </summary>
    public sealed class FileShipFittingLoadoutStore : IShipFittingLoadoutStore
    {
        private readonly string _directory;
        private readonly IReadOnlyDictionary<string, string> _builtIns;

        [Inject]
        public FileShipFittingLoadoutStore(ShipSocketsConfig config)
            : this(Path.Combine(Application.persistentDataPath, FileShipDesignStore.FolderName), BuiltIns(config))
        {
        }

        public FileShipFittingLoadoutStore(string directory, IReadOnlyDictionary<string, string> builtIns)
        {
            _directory = directory;
            // Keys are file names, which match without case on Windows and macOS.
            _builtIns = new Dictionary<string, string>(builtIns, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryLoad(string designKey, out ShipFittingLoadout loadout, out string? error)
        {
            loadout = ShipFittingLoadout.Empty;
            if (!ShipDesignKeys.IsValid(designKey))
            {
                error = $"\"{designKey}\" is not a design name.";
                return false;
            }

            if (_builtIns.TryGetValue(designKey, out var builtIn)) return Decode(designKey, builtIn, out loadout, out error);

            var path = PathOf(designKey);
            error = null;
            if (!File.Exists(path)) return true;

            try
            {
                long length = new FileInfo(path).Length;
                if (length > ShipFittingLoadoutFormat.MaxTextLength)
                {
                    error = $"{designKey}{ShipFittingLoadoutFormat.FileExtension} is {length} bytes; at most {ShipFittingLoadoutFormat.MaxTextLength}.";
                    return false;
                }

                return Decode(designKey, File.ReadAllText(path), out loadout, out error);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = $"{designKey}{ShipFittingLoadoutFormat.FileExtension} could not be read: {e.Message}";
                return false;
            }
        }

        public bool TrySave(string designKey, ShipFittingLoadout loadout, out string? error)
        {
            if (!ShipDesignKeys.IsValid(designKey))
            {
                error = $"\"{designKey}\" is not a design name.";
                return false;
            }

            if (_builtIns.ContainsKey(designKey))
            {
                error = $"{designKey} is a built-in design: save yours under another name.";
                return false;
            }

            var path = PathOf(designKey);
            var temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(temporary, ShipFittingLoadoutJsonCodec.Encode(loadout));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                error = null;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                error = $"{designKey}{ShipFittingLoadoutFormat.FileExtension} could not be saved: {e.Message}";
                return false;
            }
        }

        private static bool Decode(string designKey, string text, out ShipFittingLoadout loadout, out string? error)
        {
            if (ShipFittingLoadoutJsonCodec.TryDecode(text, out loadout, out error)) return true;
            error = $"{designKey}{ShipFittingLoadoutFormat.FileExtension}: {error}";
            return false;
        }

        private string PathOf(string designKey) => Path.Combine(_directory, designKey + ShipFittingLoadoutFormat.FileExtension);

        private static IReadOnlyDictionary<string, string> BuiltIns(ShipSocketsConfig config)
        {
            const string suffix = ".fittings";
            var builtIns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in config.BuiltInLoadouts)
            {
                if (asset == null) continue;
                // A TextAsset named "Galleon.fittings.json" on disk is called "Galleon.fittings".
                var key = asset.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? asset.name.Substring(0, asset.name.Length - suffix.Length) : asset.name;
                if (ShipDesignKeys.IsValid(key)) builtIns[key] = asset.text;
                else Debug.LogError($"[ShipSockets] Built-in loadout {asset.name} is not named after a design.", asset);
            }

            return builtIns;
        }
    }
}
