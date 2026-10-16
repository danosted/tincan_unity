#nullable enable
namespace TinCan.Features.Respawn
{
    /// <summary>This peer: what the local player last chose while overboard, and when; shared by the menu and its commands.</summary>
    public sealed class OverboardChoice
    {
        public bool Chosen { get; private set; }
        public OverboardChoiceKind? Kind { get; private set; }
        public float ChosenAt { get; private set; }

        public void Choose(OverboardChoiceKind kind, float now)
        {
            Chosen = true;
            Kind = kind;
            ChosenAt = now;
        }

        public void Clear()
        {
            Chosen = false;
            Kind = null;
        }
    }
}
