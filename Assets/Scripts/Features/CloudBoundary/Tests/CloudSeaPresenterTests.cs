#nullable enable
using NUnit.Framework;

namespace TinCan.Features.CloudBoundary.Tests
{
    public sealed class CloudSeaPresenterTests
    {
        [TestCase(3000f, 1000f, 3000f)] // Normal: the configured grid already reaches past the far plane
        [TestCase(3000f, 1500f, 3000f)] // Far: exactly reaches it
        [TestCase(3000f, 2500f, 5000f)] // Very Far: the grid grows to reach it
        public void TheSurfaceGrid_ReachesTheFarPlane(float gridSize, float farClipPlane, float expected) =>
            Assert.That(CloudSeaPresenter.SurfaceGridSize(gridSize, farClipPlane), Is.EqualTo(expected));

        [Test]
        public void TheSurface_FadesOutInsideTheFarPlane_WhenItsGridReachesPastIt() =>
            Assert.That(CloudSeaPresenter.FadeOut(CloudSeaPresenter.SurfaceGridSize(3000f, 2500f) * 0.5f, 2500f), Is.EqualTo(2375f));
    }
}
