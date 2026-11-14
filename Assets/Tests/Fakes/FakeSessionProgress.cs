#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Tests.Fakes
{
    /// <summary>A session's progress set by the test; null is no session under way.</summary>
    public sealed class FakeSessionProgress : ISessionProgress
    {
        public float? Progress { get; set; }

        public bool TryGetProgress(out float progress)
        {
            progress = Progress ?? 0f;
            return Progress != null;
        }
    }
}
