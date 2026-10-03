#nullable enable
using NUnit.Framework;
using TinCan.Core.RandomStreams;

namespace TinCan.Tests.EditMode
{
    public class RandomSourceTests
    {
        private static int[] Draw(System.Random random, int count = 8)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++) values[i] = random.Next();
            return values;
        }

        [Test]
        public void Seeded_SameStream_RepeatsAcrossSources()
        {
            var a = new RandomSource(1003).Create("SkyHazards");
            var b = new RandomSource(1003).Create("SkyHazards");

            Assert.That(Draw(a), Is.EqualTo(Draw(b)));
        }

        [Test]
        public void Seeded_StreamsAreIndependent()
        {
            var source = new RandomSource(1003);

            Assert.That(Draw(source.Create("SkyHazards")), Is.Not.EqualTo(Draw(source.Create("FlyingCans"))));
        }

        [Test]
        public void Seeded_DifferentSeedsDiffer()
        {
            Assert.That(Draw(new RandomSource(1).Create("SkyHazards")), Is.Not.EqualTo(Draw(new RandomSource(2).Create("SkyHazards"))));
        }

        [Test]
        public void FromLaunchArguments_ReadsTheSeed()
        {
            Assert.That(RandomSource.FromLaunchArguments(new[] { "game.exe", "-server", "-seed", "1003" }).Seed, Is.EqualTo(1003));
            Assert.That(RandomSource.FromLaunchArguments(new[] { "game.exe", "-server" }).Seed, Is.Null);
            Assert.That(RandomSource.FromLaunchArguments(new[] { "-seed", "soon" }).Seed, Is.Null);
        }

        [Test]
        public void StableHash_IsFixed()
        {
            // FNV-1a: must not change between runs, processes or runtimes, or seeded perf runs stop matching.
            Assert.That(RandomSource.StableHash(""), Is.EqualTo(unchecked((int)2166136261)));
            Assert.That(RandomSource.StableHash("SkyHazards"), Is.EqualTo(RandomSource.StableHash("SkyHazards")));
            Assert.That(RandomSource.StableHash("a"), Is.EqualTo(unchecked((int)0xE40C292C)));
        }
    }
}
