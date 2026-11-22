#nullable enable
namespace TinCan.Features.Camping.Tests.Fakes
{
    public sealed class FakeTentSpot : ITentSpot
    {
        public CampingConfig? Config { get; set; }
        public bool IsPitched { get; set; }
        public bool RemovesWhenPacked { get; set; }
        public bool Removed { get; private set; }

        public bool TryPitch()
        {
            if (IsPitched) return false;
            IsPitched = true;
            return true;
        }

        public bool TryPack()
        {
            if (!IsPitched) return false;
            IsPitched = false;
            return true;
        }

        public void Remove() => Removed = true;
    }
}
