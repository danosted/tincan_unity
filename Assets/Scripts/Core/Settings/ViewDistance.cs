#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Core.Settings
{
    /// <summary>
    /// How far the game draws (the local view camera's far plane): the graphics.view_distance choices and their metres.
    /// Everything that fades with distance follows the far plane on its own: the air turns a surface wholly into air
    /// before it (Shaders/Include/TinCanAtmosphere.hlsl) and the drawn cloud sea grows its grid to reach it
    /// (CloudSeaPresenter). Normal is the far plane the cameras were built with.
    /// </summary>
    public static class ViewDistance
    {
        public const string Key = "graphics.view_distance";
        public const string Default = "Normal";

        public static readonly IReadOnlyList<(string Name, float Metres)> Options = new[]
        {
            ("Near", 700f),
            ("Normal", 1000f),
            ("Far", 1500f),
            ("Very Far", 2500f),
        };

        public static string[] Names => Options.Select(option => option.Name).ToArray();

        /// <summary>The metres for a choice; Normal's for none or an unknown one.</summary>
        public static float Metres(string? choice)
        {
            float normal = 0f;
            foreach (var (name, metres) in Options)
            {
                if (string.Equals(name, choice, StringComparison.OrdinalIgnoreCase)) return metres;
                if (name == Default) normal = metres;
            }

            return normal;
        }
    }
}
