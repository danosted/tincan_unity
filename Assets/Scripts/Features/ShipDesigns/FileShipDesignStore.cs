#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VContainer;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Designs as <c>*.ship.json</c> files in <c>persistentDataPath/Ships</c>, plus the built-in designs listed in
    /// <see cref="ShipDesignsConfig"/> (TextAssets). Saves write a temporary file and swap it in, so a crash never leaves
    /// half a design. Files over the size limit are refused before they are read.
    /// </summary>
    public sealed class FileShipDesignStore : IShipDesignStore
    {
        private const string FolderName = "Ships";

        private readonly string _directory;
        private readonly IReadOnlyDictionary<string, string> _builtIns;
        private readonly IShipDesignCodec _codec;
        private readonly int _maxTextLength;

        [Inject]
        public FileShipDesignStore(ShipDesignsConfig config, IShipDesignCodec codec)
            : this(Path.Combine(Application.persistentDataPath, FolderName), BuiltIns(config), codec, config.Limits.MaxTextLength)
        {
        }

        public FileShipDesignStore(string directory, IReadOnlyDictionary<string, string> builtIns, IShipDesignCodec codec, int maxTextLength)
        {
            _directory = directory;
            _builtIns = builtIns;
            _codec = codec;
            _maxTextLength = maxTextLength;
        }

        public string Directory => _directory;

        public IReadOnlyList<ShipDesignListing> List()
        {
            var saved = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (System.IO.Directory.Exists(_directory))
            {
                foreach (var path in System.IO.Directory.GetFiles(_directory, "*" + ShipDesignFormat.FileExtension))
                {
                    var key = KeyOf(path);
                    if (ShipDesignKeys.IsValid(key)) saved.Add(key);
                }
            }

            return _builtIns.Keys.Where(k => !saved.Contains(k)).OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .Select(k => new ShipDesignListing(k, true))
                .Concat(saved.Select(k => new ShipDesignListing(k, false)))
                .ToList();
        }

        public ShipDesignDecodeResult Load(string key)
        {
            if (!ShipDesignKeys.IsValid(key)) return ShipDesignDecodeResult.Failed($"\"{key}\" is not a design name.");

            var path = PathOf(key);
            if (File.Exists(path))
            {
                try
                {
                    long length = new FileInfo(path).Length;
                    if (length > _maxTextLength) return ShipDesignDecodeResult.Failed($"{key} is {length} bytes; at most {_maxTextLength}.");
                    return _codec.Decode(File.ReadAllText(path));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    return ShipDesignDecodeResult.Failed($"{key} could not be read: {e.Message}");
                }
            }

            return _builtIns.TryGetValue(key, out var text)
                ? _codec.Decode(text)
                : ShipDesignDecodeResult.Failed($"There is no design called {key}.");
        }

        public bool TrySave(string key, ShipDesign design, out string? error)
        {
            if (!ShipDesignKeys.IsValid(key))
            {
                error = $"\"{key}\" is not a design name.";
                return false;
            }

            var path = PathOf(key);
            var temporary = path + ".tmp";
            try
            {
                System.IO.Directory.CreateDirectory(_directory);
                File.WriteAllText(temporary, _codec.Encode(design));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                error = null;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                error = $"{key} could not be saved: {e.Message}";
                return false;
            }
        }

        public bool TryDelete(string key)
        {
            if (!ShipDesignKeys.IsValid(key)) return false;

            var path = PathOf(key);
            if (!File.Exists(path)) return false;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private string PathOf(string key) => Path.Combine(_directory, key + ShipDesignFormat.FileExtension);

        private static string KeyOf(string path)
        {
            var file = Path.GetFileName(path);
            return file.Substring(0, file.Length - ShipDesignFormat.FileExtension.Length);
        }

        private static IReadOnlyDictionary<string, string> BuiltIns(ShipDesignsConfig config)
        {
            var builtIns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in config.BuiltInDesigns)
            {
                if (asset == null) continue;
                // A TextAsset named "Starter.ship.json" on disk is called "Starter.ship".
                var key = asset.name.EndsWith(".ship", StringComparison.OrdinalIgnoreCase) ? asset.name.Substring(0, asset.name.Length - 5) : asset.name;
                if (ShipDesignKeys.IsValid(key)) builtIns[key] = asset.text;
                else Debug.LogError($"[ShipDesigns] Built-in design {asset.name} has no valid name.", asset);
            }

            return builtIns;
        }
    }
}
