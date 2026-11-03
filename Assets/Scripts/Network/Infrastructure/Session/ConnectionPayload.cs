#nullable enable
using System;
using System.IO;
using System.Text;

namespace TinCan.Network.Infrastructure.Session
{
    /// <summary>
    /// What a client sends when it connects: the build's protocol version and the player's name. A format byte leads, so
    /// a later payload (a password hash) can be told apart from this one.
    /// </summary>
    public readonly struct ConnectionPayload
    {
        public const byte Format = 1;
        public const int MaxNameLength = 24;
        public const string DefaultName = "Player";

        public readonly string Version;
        public readonly string Name;

        public ConnectionPayload(string version, string name)
        {
            Version = version ?? string.Empty;
            Name = CleanName(name);
        }

        public byte[] Encode()
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Format);
                writer.Write(Version);
                writer.Write(Name);
            }
            return stream.ToArray();
        }

        /// <summary>Reads a payload; false for an empty, truncated or other-format one (an older or foreign build).</summary>
        public static bool TryDecode(byte[]? bytes, out ConnectionPayload payload)
        {
            payload = default;
            if (bytes == null || bytes.Length == 0 || bytes[0] != Format) return false;

            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
                reader.ReadByte();
                payload = new ConnectionPayload(reader.ReadString(), reader.ReadString());
                return true;
            }
            catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Trimmed, control characters dropped, cut to <see cref="MaxNameLength"/>; <see cref="DefaultName"/> when nothing is left.</summary>
        public static string CleanName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return DefaultName;

            var builder = new StringBuilder(name!.Length);
            foreach (char c in name)
            {
                if (!char.IsControl(c)) builder.Append(c);
            }

            string cleaned = builder.ToString().Trim();
            if (cleaned.Length > MaxNameLength) cleaned = cleaned.Substring(0, MaxNameLength).TrimEnd();
            return cleaned.Length == 0 ? DefaultName : cleaned;
        }
    }
}
