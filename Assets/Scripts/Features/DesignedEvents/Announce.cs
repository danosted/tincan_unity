#nullable enable
namespace TinCan.Features.DesignedEvents
{
    /// <summary>Event action: show a line to the crew (the HUD's "Event" line).</summary>
    public sealed class Announce : IEventAction
    {
        public Announce(string text) => Text = text;

        public string Text { get; }

        public override string ToString() => $"Announce \"{Text}\"";
    }
}
