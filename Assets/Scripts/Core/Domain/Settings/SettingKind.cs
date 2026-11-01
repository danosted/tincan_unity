#nullable enable
namespace TinCan.Core.Domain.Settings
{
    /// <summary>How a setting's value is edited and checked (<see cref="SettingDefinition"/>).</summary>
    public enum SettingKind
    {
        /// <summary>A number between a minimum and a maximum, in steps.</summary>
        Slider,
        Toggle,
        /// <summary>One of a fixed list of options.</summary>
        Choice,
        /// <summary>Free text up to a maximum length.</summary>
        Text
    }
}
