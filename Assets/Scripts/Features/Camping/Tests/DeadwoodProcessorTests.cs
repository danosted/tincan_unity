#nullable enable
using NUnit.Framework;

namespace TinCan.Features.Camping.Tests
{
    public class DeadwoodProcessorTests
    {
        private const int Size = 3;
        private const float Regrow = 100f;

        [Test]
        public void Settle_AFullPile_StaysFull()
        {
            var settled = DeadwoodProcessor.Settle(stock: 3, at: 0d, now: 500d, Size, Regrow);

            Assert.That(settled.Stock, Is.EqualTo(3));
        }

        [Test]
        public void Settle_GathersOneLogPerInterval_KeepingThePartProgress()
        {
            var settled = DeadwoodProcessor.Settle(stock: 0, at: 0d, now: 250d, Size, Regrow);

            Assert.That(settled.Stock, Is.EqualTo(2));
            Assert.That(settled.At, Is.EqualTo(200d), "the half interval toward the third log is kept");
        }

        [Test]
        public void Settle_NeverGrowsPastTheSize()
        {
            var settled = DeadwoodProcessor.Settle(stock: 1, at: 0d, now: 10_000d, Size, Regrow);

            Assert.That(settled.Stock, Is.EqualTo(Size));
        }

        [Test]
        public void Take_FromAFullPile_StartsRegrowingNow()
        {
            var taken = DeadwoodProcessor.Take(stock: 3, at: 0d, now: 40d, Size, Regrow);

            Assert.That(taken, Is.Not.Null);
            Assert.That(taken!.Value.Stock, Is.EqualTo(2));
            Assert.That(taken.Value.At, Is.EqualTo(40d));
        }

        [Test]
        public void Take_FromABarePile_IsRefused()
        {
            Assert.That(DeadwoodProcessor.Take(stock: 0, at: 0d, now: 50d, Size, Regrow), Is.Null);
        }

        [Test]
        public void Take_AfterRegrowth_TakesTheGrownLog()
        {
            var taken = DeadwoodProcessor.Take(stock: 0, at: 0d, now: 120d, Size, Regrow);

            Assert.That(taken!.Value.Stock, Is.EqualTo(0));
            Assert.That(taken.Value.At, Is.EqualTo(100d));
        }

        [Test]
        public void Return_PutsALogBack_UpToTheSize()
        {
            Assert.That(DeadwoodProcessor.Return(stock: 1, at: 0d, now: 50d, Size, Regrow).Stock, Is.EqualTo(2));
            Assert.That(DeadwoodProcessor.Return(stock: 3, at: 0d, now: 50d, Size, Regrow).Stock, Is.EqualTo(3));
        }
    }
}
