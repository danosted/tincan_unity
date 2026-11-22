#nullable enable
namespace TinCan.Features.Camping.Tests.Fakes
{
    /// <summary>A deadwood pile that is a plain count: no regrowth.</summary>
    public sealed class FakeDeadwood : IDeadwood
    {
        public CampingConfig? Config { get; set; }
        public int Available { get; set; }

        public bool TryTake()
        {
            if (Available <= 0) return false;
            Available--;
            return true;
        }

        public void Return() => Available++;
    }
}
