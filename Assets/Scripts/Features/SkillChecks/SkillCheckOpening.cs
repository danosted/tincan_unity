#nullable enable
namespace TinCan.Features.SkillChecks
{
    /// <summary>A check the server opened for this peer's player: its number (sent back with the grade) and where the band is.</summary>
    public readonly struct SkillCheckOpening
    {
        public readonly int CheckId;
        public readonly float BandCenter;

        public SkillCheckOpening(int checkId, float bandCenter)
        {
            CheckId = checkId;
            BandCenter = bandCenter;
        }
    }
}
