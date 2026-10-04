#nullable enable
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A stored design's key is its file name without <see cref="ShipDesignFormat.FileExtension"/>: letters, digits,
    /// spaces, '-' and '_', so it is a safe file name on every platform and cannot leave the designs folder.
    /// </summary>
    public static class ShipDesignKeys
    {
        public const int MaxLength = 64;
        private static readonly Regex Format = new(@"^[A-Za-z0-9_\-]([A-Za-z0-9 _\-]*[A-Za-z0-9_\-])?$");

        public static bool IsValid(string? key) => !string.IsNullOrEmpty(key) && key!.Length <= MaxLength && Format.IsMatch(key);

        /// <summary>A valid key made from a design's name: other characters become '_'.</summary>
        public static string FromName(string? name)
        {
            var key = new StringBuilder();
            foreach (char c in (name ?? string.Empty).Trim().Take(MaxLength))
            {
                key.Append(char.IsLetterOrDigit(c) && c < 128 || c is ' ' or '-' or '_' ? c : '_');
            }

            var result = key.ToString().Trim();
            return IsValid(result) ? result : "Ship";
        }
    }
}
