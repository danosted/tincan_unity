#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Settings
{
    /// <summary>
    /// The look settings every mouse-driven view applies: one sensitivity that scales the view's own tuned value (so the
    /// cannon keeps its slower feel), and invert Y. Without a settings service the view's tuned value is used as it is.
    /// </summary>
    public static class LookSettings
    {
        public const string SensitivityKey = "look.sensitivity";
        public const string InvertYKey = "look.invert_y";

        /// <summary>A raw look delta as the player set it up: scaled, and with Y flipped when inverted.</summary>
        public static Vector2 Apply(IPlayerSettings? settings, Vector2 delta)
        {
            if (settings == null) return delta;
            float scale = settings.Has(SensitivityKey) ? settings.GetFloat(SensitivityKey) : 1f;
            bool invert = settings.Has(InvertYKey) && settings.GetBool(InvertYKey);
            return new Vector2(delta.x * scale, invert ? -delta.y * scale : delta.y * scale);
        }
    }
}
