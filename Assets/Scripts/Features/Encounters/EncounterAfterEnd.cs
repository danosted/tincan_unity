#nullable enable
namespace TinCan.Features.Encounters
{
    /// <summary>
    /// What a zone does once its round ends (cured, failed or abandoned). A setting, not decided yet (developer,
    /// 2026-11-23: keep it flexible).
    /// </summary>
    public enum EncounterAfterEnd
    {
        /// <summary>Rests for <see cref="EncounterZoneConfig.RestSeconds"/>, then can be entered again.</summary>
        Rest,

        /// <summary>Spent for good in this world.</summary>
        Gone
    }
}
