#nullable enable
namespace TinCan.Features.CloudBoundary
{
    /// <summary>How many wisps stream past the view, and how visible they are (<see cref="CloudWispsProcessor"/>).</summary>
    public readonly struct CloudWispsFrame
    {
        public CloudWispsFrame(float rate, float opacity)
        {
            Rate = rate;
            Opacity = opacity;
        }

        /// <summary>Wisps per second.</summary>
        public float Rate { get; }

        /// <summary>0..1.</summary>
        public float Opacity { get; }
    }
}
