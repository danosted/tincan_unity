#nullable enable

namespace TinCan.Core.Domain.Audio
{
    /// <summary>
    /// Which mixer group a sound goes through (<c>Assets/Audio/TinCanMixer.mixer</c>), so it can be balanced and turned
    /// down with the rest of its kind. See .docs/plans/audio-mixer.md.
    /// </summary>
    public enum AudioChannel
    {
        /// <summary>Gameplay sound in the world, in 3D.</summary>
        World = 0,

        /// <summary>Menus and HUD toasts, in 2D.</summary>
        Ui = 1,

        /// <summary>Wind and cloud beds.</summary>
        Ambience = 2,

        Music = 3,

        /// <summary>Reserved for voice chat.</summary>
        Voice = 4,
    }
}
