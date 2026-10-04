#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The design file format: JSON, one line per part so files diff well. Written in canonical form (parts in instance
    /// id order, fixed field order, "\n" line ends), so a design always produces the same text.
    /// Reading: the text must fit the limits; a file newer than <see cref="ShipDesignFormat.CurrentVersion"/> is refused,
    /// an older one migrated first. Fields this build does not know are kept as raw JSON and written back, at the top
    /// level and on each part, so a file from a later build survives a load and save. Plan:
    /// .docs/plans/modular-airship-builder.md.
    /// </summary>
    public sealed class ShipDesignJsonCodec : IShipDesignCodec
    {
        private static readonly HashSet<string> TopLevelFields = new()
        {
            ShipDesignFormat.FormatVersionField, ShipDesignFormat.NameField, ShipDesignFormat.AuthorField,
            ShipDesignFormat.NextPartInstanceIdField, ShipDesignFormat.PartsField,
        };

        private static readonly HashSet<string> PartFields = new()
        {
            ShipDesignFormat.PartInstanceIdField, ShipDesignFormat.PartIdField, ShipDesignFormat.PartCellField,
            ShipDesignFormat.PartOrientationField,
        };

        private readonly ShipDesignLimits _limits;
        private readonly ShipDesignMigrator _migrator;

        public ShipDesignJsonCodec(ShipDesignLimits limits, ShipDesignMigrator? migrator = null)
        {
            _limits = limits;
            _migrator = migrator ?? ShipDesignMigrator.Current;
        }

        public string Encode(ShipDesign design)
        {
            using var text = new StringWriter { NewLine = "\n" };
            using var writer = new JsonTextWriter(text) { Formatting = Formatting.Indented, Indentation = 2 };

            writer.WriteStartObject();
            writer.WritePropertyName(ShipDesignFormat.FormatVersionField);
            writer.WriteValue(ShipDesignFormat.CurrentVersion);
            writer.WritePropertyName(ShipDesignFormat.NameField);
            writer.WriteValue(design.Name);
            writer.WritePropertyName(ShipDesignFormat.AuthorField);
            writer.WriteValue(design.Author);
            writer.WritePropertyName(ShipDesignFormat.NextPartInstanceIdField);
            writer.WriteValue(design.NextPartInstanceId);
            WriteExtensions(writer, design.Extensions);

            writer.WritePropertyName(ShipDesignFormat.PartsField);
            writer.WriteStartArray();
            foreach (var part in design.Parts.OrderBy(p => p.InstanceId)) WritePart(writer, part);
            writer.WriteEndArray();

            writer.WriteEndObject();
            writer.Flush();
            return text.ToString() + "\n";
        }

        public ShipDesignDecodeResult Decode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return ShipDesignDecodeResult.Failed("The design file is empty.");
            if (text.Length > _limits.MaxTextLength)
            {
                return ShipDesignDecodeResult.Failed($"The design file is {text.Length} characters; at most {_limits.MaxTextLength}.");
            }

            try
            {
                return Decode(Parse(text));
            }
            catch (Exception e) when (e is JsonException or FormatException or OverflowException or InvalidCastException)
            {
                return ShipDesignDecodeResult.Failed($"The design file is not readable: {e.Message}");
            }
        }

        private ShipDesignDecodeResult Decode(JObject file)
        {
            int version = ReadInt(file, ShipDesignFormat.FormatVersionField, "the file");
            if (version < 1) return ShipDesignDecodeResult.Failed($"Format version {version} does not exist.");
            if (version > ShipDesignFormat.CurrentVersion)
            {
                return ShipDesignDecodeResult.Failed(
                    $"The design was saved by a newer game (format version {version}; this game reads up to {ShipDesignFormat.CurrentVersion}).");
            }

            if (!_migrator.TryMigrate(file, version, out var migrationError)) return ShipDesignDecodeResult.Failed(migrationError!);

            if (file[ShipDesignFormat.PartsField] is not JArray partsArray)
            {
                return ShipDesignDecodeResult.Failed($"The file has no \"{ShipDesignFormat.PartsField}\" list.");
            }

            if (partsArray.Count > _limits.MaxParts)
            {
                return ShipDesignDecodeResult.Failed($"The design has {partsArray.Count} parts; at most {_limits.MaxParts}.");
            }

            var parts = new List<ShipPartPlacement>(partsArray.Count);
            for (int i = 0; i < partsArray.Count; i++)
            {
                if (partsArray[i] is not JObject part) return ShipDesignDecodeResult.Failed($"Part {i} is not an object.");
                parts.Add(ReadPart(part, i));
            }

            string name = ReadString(file, ShipDesignFormat.NameField, "the file") ?? string.Empty;
            string author = ReadString(file, ShipDesignFormat.AuthorField, "the file") ?? string.Empty;
            int nextId = file.ContainsKey(ShipDesignFormat.NextPartInstanceIdField)
                ? ReadInt(file, ShipDesignFormat.NextPartInstanceIdField, "the file")
                : (parts.Count == 0 ? 1 : parts.Max(p => p.InstanceId) + 1);

            return ShipDesignDecodeResult.Read(new ShipDesign(name, author, nextId, parts, Extensions(file, TopLevelFields)));
        }

        private static JObject Parse(string text)
        {
            using var reader = new JsonTextReader(new StringReader(text))
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 64,
            };

            var token = JToken.ReadFrom(reader);
            if (reader.Read()) throw new JsonReaderException("There is more after the design.");
            return token as JObject ?? throw new FormatException("The file is not a JSON object.");
        }

        private static ShipPartPlacement ReadPart(JObject part, int index)
        {
            string where = $"part {index}";
            int instanceId = ReadInt(part, ShipDesignFormat.PartInstanceIdField, where);
            string partId = ReadString(part, ShipDesignFormat.PartIdField, where)
                            ?? throw new FormatException($"{where} has no \"{ShipDesignFormat.PartIdField}\".");

            if (part[ShipDesignFormat.PartCellField] is not JArray cell || cell.Count != 3)
            {
                throw new FormatException($"{where}: \"{ShipDesignFormat.PartCellField}\" must be three whole numbers.");
            }

            int orientation = part.ContainsKey(ShipDesignFormat.PartOrientationField)
                ? ReadInt(part, ShipDesignFormat.PartOrientationField, where)
                : 0;
            if (orientation is < 0 or > byte.MaxValue) throw new FormatException($"{where}: orientation {orientation} is out of range.");

            return new ShipPartPlacement(instanceId, partId,
                new ShipGridCell(ToInt(cell[0], where), ToInt(cell[1], where), ToInt(cell[2], where)),
                (byte)orientation, Extensions(part, PartFields));
        }

        private static void WritePart(JsonWriter writer, ShipPartPlacement part)
        {
            writer.WriteStartObject();
            // One line per part: compact inside the object, indented between parts.
            writer.Formatting = Formatting.None;
            writer.WritePropertyName(ShipDesignFormat.PartInstanceIdField);
            writer.WriteValue(part.InstanceId);
            writer.WritePropertyName(ShipDesignFormat.PartIdField);
            writer.WriteValue(part.PartId);
            writer.WritePropertyName(ShipDesignFormat.PartCellField);
            writer.WriteStartArray();
            writer.WriteValue(part.Cell.X);
            writer.WriteValue(part.Cell.Y);
            writer.WriteValue(part.Cell.Z);
            writer.WriteEndArray();
            writer.WritePropertyName(ShipDesignFormat.PartOrientationField);
            writer.WriteValue(part.Orientation);
            WriteExtensions(writer, part.Extensions);
            writer.WriteEndObject();
            writer.Formatting = Formatting.Indented;
        }

        private static void WriteExtensions(JsonWriter writer, IReadOnlyDictionary<string, string> extensions)
        {
            foreach (var pair in extensions)
            {
                writer.WritePropertyName(pair.Key);
                writer.WriteRawValue(pair.Value);
            }
        }

        private static IReadOnlyDictionary<string, string>? Extensions(JObject source, HashSet<string> known)
        {
            Dictionary<string, string>? extensions = null;
            foreach (var property in source.Properties())
            {
                if (known.Contains(property.Name)) continue;
                extensions ??= new Dictionary<string, string>();
                extensions[property.Name] = property.Value.ToString(Formatting.None);
            }

            return extensions;
        }

        private static int ReadInt(JObject source, string field, string where) =>
            source[field] is { } token ? ToInt(token, $"{where}, \"{field}\"") : throw new FormatException($"{where} has no \"{field}\".");

        private static int ToInt(JToken token, string where) =>
            token.Type == JTokenType.Integer ? checked((int)token.Value<long>()) : throw new FormatException($"{where} is not a whole number.");

        private static string? ReadString(JObject source, string field, string where) => source[field] switch
        {
            null => null,
            { Type: JTokenType.String } token => token.Value<string>(),
            _ => throw new FormatException($"{where}: \"{field}\" is not text."),
        };
    }
}
