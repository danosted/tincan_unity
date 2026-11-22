#nullable enable
namespace TinCan.Features.Camping.Tests.Fakes
{
    /// <summary>A fire with the real stoke rule at a fixed time: Now is 0, so BurnSecondsLeft is what it holds.</summary>
    public sealed class FakeFire : IFire
    {
        public CampingConfig? Config { get; set; }
        public float BurnSecondsLeft { get; set; }
        public int StokeCalls { get; private set; }

        public float Stoke(float seconds)
        {
            StokeCalls++;
            if (Config == null) return 0f;

            var (outAt, accepted) = FireProcessor.Stoke(0d, BurnSecondsLeft, seconds, Config.MaxBurnSeconds);
            BurnSecondsLeft = (float)outAt;
            return accepted;
        }
    }
}
