#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The compact form the server sends to every peer: a part-id palette (each part type once), then per part its
    /// instance id, palette index, cell (zig-zag varints) and orientation, about 6 bytes a part. Canonical: parts in
    /// instance id order, palette in order of first use, so equal designs give equal bytes (<see cref="ShipDesignHash"/>).
    /// It carries what building needs, not <see cref="ShipDesign.Extensions"/>: peers build ships, they never save the
    /// server's file. Both ends run the same build, so it has its own version byte instead of migrations.
    /// </summary>
    public static class ShipDesignBinaryCodec
    {
        private const byte Version = 1;
        private const int MaxStringBytes = 1024;

        public static byte[] Encode(ShipDesign design)
        {
            var parts = design.Parts.OrderBy(p => p.InstanceId).ToList();
            var palette = new List<string>();
            var paletteIndex = new Dictionary<string, int>();
            foreach (var part in parts)
            {
                if (paletteIndex.ContainsKey(part.PartId)) continue;
                paletteIndex.Add(part.PartId, palette.Count);
                palette.Add(part.PartId);
            }

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8);
            writer.Write(Version);
            WriteString(writer, design.Name);
            WriteString(writer, design.Author);
            WriteVarInt(writer, (uint)design.NextPartInstanceId);

            WriteVarInt(writer, (uint)palette.Count);
            foreach (var partId in palette) WriteString(writer, partId);

            WriteVarInt(writer, (uint)parts.Count);
            foreach (var part in parts)
            {
                WriteVarInt(writer, ZigZag(part.InstanceId));
                WriteVarInt(writer, (uint)paletteIndex[part.PartId]);
                WriteVarInt(writer, ZigZag(part.Cell.X));
                WriteVarInt(writer, ZigZag(part.Cell.Y));
                WriteVarInt(writer, ZigZag(part.Cell.Z));
                writer.Write(part.Orientation);
            }

            writer.Flush();
            return stream.ToArray();
        }

        public static ShipDesignDecodeResult Decode(byte[] bytes, ShipDesignLimits limits)
        {
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
                byte version = reader.ReadByte();
                if (version != Version) return ShipDesignDecodeResult.Failed($"Unknown network design version {version}.");

                string name = ReadString(reader);
                string author = ReadString(reader);
                int nextId = checked((int)ReadVarInt(reader));

                int paletteCount = checked((int)ReadVarInt(reader));
                if (paletteCount > limits.MaxParts) return ShipDesignDecodeResult.Failed("The design names too many part types.");
                var palette = new string[paletteCount];
                for (int i = 0; i < paletteCount; i++) palette[i] = ReadString(reader);

                int count = checked((int)ReadVarInt(reader));
                if (count > limits.MaxParts) return ShipDesignDecodeResult.Failed($"The design has {count} parts; at most {limits.MaxParts}.");

                var parts = new List<ShipPartPlacement>(count);
                for (int i = 0; i < count; i++)
                {
                    int instanceId = UnZigZag(ReadVarInt(reader));
                    uint index = ReadVarInt(reader);
                    if (index >= palette.Length) return ShipDesignDecodeResult.Failed($"Part {i} names part type {index}, which is not listed.");
                    var cell = new ShipGridCell(UnZigZag(ReadVarInt(reader)), UnZigZag(ReadVarInt(reader)), UnZigZag(ReadVarInt(reader)));
                    parts.Add(new ShipPartPlacement(instanceId, palette[index], cell, reader.ReadByte()));
                }

                if (reader.BaseStream.Position != reader.BaseStream.Length) return ShipDesignDecodeResult.Failed("There is more after the design.");
                return ShipDesignDecodeResult.Read(new ShipDesign(name, author, nextId, parts));
            }
            catch (Exception e) when (e is EndOfStreamException or OverflowException or FormatException or ArgumentException)
            {
                return ShipDesignDecodeResult.Failed($"The network design is not readable: {e.Message}");
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WriteVarInt(writer, (uint)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = checked((int)ReadVarInt(reader));
            if (length > MaxStringBytes) throw new FormatException($"A text of {length} bytes is too long.");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }

        private static void WriteVarInt(BinaryWriter writer, uint value)
        {
            while (value >= 0x80)
            {
                writer.Write((byte)(value | 0x80));
                value >>= 7;
            }

            writer.Write((byte)value);
        }

        private static uint ReadVarInt(BinaryReader reader)
        {
            uint value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = reader.ReadByte();
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
            }

            throw new FormatException("A number is too long.");
        }

        private static uint ZigZag(int value) => (uint)((value << 1) ^ (value >> 31));

        private static int UnZigZag(uint value) => (int)(value >> 1) ^ -(int)(value & 1);
    }
}
