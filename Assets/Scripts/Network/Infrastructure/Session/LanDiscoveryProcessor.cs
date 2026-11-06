#nullable enable
using System;
using System.IO;
using System.Text;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// The discovery datagrams. A browser broadcasts a query; every host and dedicated server answers it, to the
    /// sender, with a reply. Both start with a four-byte tag and a format byte, so anything else on the port is ignored.
    /// Names are cut so a reply stays well under one datagram.
    /// </summary>
    public static class LanDiscoveryProcessor
    {
        public const ushort DiscoveryPort = 47777;
        public const byte Format = 1;
        public const int MaxNameLength = 40;
        public const int MaxVersionLength = 32;

        private static readonly byte[] QueryTag = { (byte)'T', (byte)'C', (byte)'D', (byte)'Q' };
        private static readonly byte[] ReplyTag = { (byte)'T', (byte)'C', (byte)'D', (byte)'R' };

        public static byte[] Query()
        {
            var bytes = new byte[QueryTag.Length + 1];
            QueryTag.CopyTo(bytes, 0);
            bytes[QueryTag.Length] = Format;
            return bytes;
        }

        public static bool IsQuery(byte[]? bytes) => HasHeader(bytes, QueryTag) && bytes!.Length == QueryTag.Length + 1;

        public static byte[] Reply(LanGameInfo info)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(ReplyTag);
                writer.Write(Format);
                writer.Write(info.HostId);
                writer.Write(Cut(info.Name, MaxNameLength));
                writer.Write(Cut(info.Version, MaxVersionLength));
                writer.Write(info.GamePort);
                writer.Write((byte)Math.Clamp(info.Players, 0, byte.MaxValue));
                writer.Write((byte)Math.Clamp(info.MaxPlayers, 0, byte.MaxValue));
                writer.Write(info.NeedsPassword);
            }
            return stream.ToArray();
        }

        /// <summary>Reads a reply; false for a query, another format, a truncated reply or anything else.</summary>
        public static bool TryReadReply(byte[]? bytes, out LanGameInfo info)
        {
            info = default;
            if (!HasHeader(bytes, ReplyTag)) return false;

            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes!), Encoding.UTF8);
                reader.ReadBytes(ReplyTag.Length + 1);
                uint hostId = reader.ReadUInt32();
                string name = reader.ReadString();
                string version = reader.ReadString();
                ushort port = reader.ReadUInt16();
                int players = reader.ReadByte();
                int maxPlayers = reader.ReadByte();
                bool password = reader.ReadBoolean();
                if (port == 0) return false;

                info = new LanGameInfo(hostId, Cut(name, MaxNameLength), Cut(version, MaxVersionLength), port, players, maxPlayers, password);
                return true;
            }
            catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException)
            {
                return false;
            }
        }

        /// <summary>A game not heard from for this long is gone (a browser asks about once a second).</summary>
        public static bool IsStale(float lastSeen, float now, float timeout) => now - lastSeen > timeout;

        private static bool HasHeader(byte[]? bytes, byte[] tag)
        {
            if (bytes == null || bytes.Length < tag.Length + 1) return false;
            for (int i = 0; i < tag.Length; i++)
            {
                if (bytes[i] != tag[i]) return false;
            }
            return bytes[tag.Length] == Format;
        }

        private static string Cut(string? text, int length)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text!.Length <= length ? text : text.Substring(0, length);
        }
    }
}
