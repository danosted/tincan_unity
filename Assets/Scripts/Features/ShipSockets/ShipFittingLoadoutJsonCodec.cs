#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// The loadout file format (<see cref="ShipFittingLoadoutFormat"/>): JSON, one line per fitting, written in canonical
    /// form (socket order, fixed field order, "\n" line ends). Reading refuses a file over the limits, from a newer game,
    /// or naming one socket twice. Whether a socket or fitting exists is not its business: the server refuses those when
    /// it mounts (<see cref="ShipFittingUseCase"/>), so a loadout survives a design edit that removed a part.
    /// </summary>
    public static class ShipFittingLoadoutJsonCodec
    {
        public static string Encode(ShipFittingLoadout loadout)
        {
            using var text = new StringWriter { NewLine = "\n" };
            using var writer = new JsonTextWriter(text) { Formatting = Formatting.Indented, Indentation = 2 };

            writer.WriteStartObject();
            writer.WritePropertyName(ShipFittingLoadoutFormat.FormatVersionField);
            writer.WriteValue(ShipFittingLoadoutFormat.CurrentVersion);
            writer.WritePropertyName(ShipFittingLoadoutFormat.FittingsField);
            writer.WriteStartArray();
            foreach (var fitting in loadout.Fittings)
            {
                writer.WriteStartObject();
                // One line per fitting: compact inside the object, indented between fittings.
                writer.Formatting = Formatting.None;
                writer.WritePropertyName(ShipFittingLoadoutFormat.PartField);
                writer.WriteValue(fitting.Socket.PartInstanceId);
                writer.WritePropertyName(ShipFittingLoadoutFormat.SocketField);
                writer.WriteValue(fitting.Socket.Index);
                writer.WritePropertyName(ShipFittingLoadoutFormat.FittingField);
                writer.WriteValue(fitting.FittingId);
                writer.WriteEndObject();
                writer.Formatting = Formatting.Indented;
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            return text.ToString() + "\n";
        }

        public static bool TryDecode(string text, out ShipFittingLoadout loadout, out string? error)
        {
            loadout = ShipFittingLoadout.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "The loadout file is empty.";
                return false;
            }

            if (text.Length > ShipFittingLoadoutFormat.MaxTextLength)
            {
                error = $"The loadout file is {text.Length} characters; at most {ShipFittingLoadoutFormat.MaxTextLength}.";
                return false;
            }

            try
            {
                error = Decode(Parse(text), out loadout);
                return error == null;
            }
            catch (Exception e) when (e is JsonException or FormatException or OverflowException or InvalidCastException)
            {
                error = $"The loadout file is not readable: {e.Message}";
                return false;
            }
        }

        private static string? Decode(JObject file, out ShipFittingLoadout loadout)
        {
            loadout = ShipFittingLoadout.Empty;
            int version = ReadInt(file, ShipFittingLoadoutFormat.FormatVersionField, "the file");
            if (version < 1) return $"Format version {version} does not exist.";
            if (version > ShipFittingLoadoutFormat.CurrentVersion)
            {
                return $"The loadout was saved by a newer game (format version {version}; this game reads up to {ShipFittingLoadoutFormat.CurrentVersion}).";
            }

            if (file[ShipFittingLoadoutFormat.FittingsField] is not JArray entries) return $"The file has no \"{ShipFittingLoadoutFormat.FittingsField}\" list.";
            if (entries.Count > ShipFittingLoadoutFormat.MaxFittings) return $"The loadout has {entries.Count} fittings; at most {ShipFittingLoadoutFormat.MaxFittings}.";

            var fittings = new List<ShipFittingPlacement>(entries.Count);
            var sockets = new HashSet<ShipSocketId>();
            for (int i = 0; i < entries.Count; i++)
            {
                string where = $"fitting {i}";
                if (entries[i] is not JObject entry) return $"{where} is not an object.";

                var socket = new ShipSocketId(ReadInt(entry, ShipFittingLoadoutFormat.PartField, where),
                    ReadInt(entry, ShipFittingLoadoutFormat.SocketField, where));
                if (entry[ShipFittingLoadoutFormat.FittingField] is not { Type: JTokenType.String } id || string.IsNullOrEmpty(id.Value<string>()))
                {
                    return $"{where} has no \"{ShipFittingLoadoutFormat.FittingField}\".";
                }

                if (!sockets.Add(socket)) return $"{where}: {socket} already has a fitting.";
                fittings.Add(new ShipFittingPlacement(socket, id.Value<string>()!));
            }

            loadout = new ShipFittingLoadout(fittings);
            return null;
        }

        private static JObject Parse(string text)
        {
            using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
            var token = JToken.ReadFrom(reader);
            if (reader.Read()) throw new JsonReaderException("There is more after the loadout.");
            return token as JObject ?? throw new FormatException("The file is not a JSON object.");
        }

        private static int ReadInt(JObject source, string field, string where) => source[field] switch
        {
            { Type: JTokenType.Integer } token => checked((int)token.Value<long>()),
            null => throw new FormatException($"{where} has no \"{field}\"."),
            _ => throw new FormatException($"{where}, \"{field}\" is not a whole number."),
        };
    }
}
