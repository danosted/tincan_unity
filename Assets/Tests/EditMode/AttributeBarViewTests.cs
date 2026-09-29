#nullable enable
using NUnit.Framework;
using TinCan.Core.Gas.Views;

namespace TinCan.Tests.EditMode
{
    /// <summary>Covers AttributeBarView's math: how full the bar is, and when it shows.</summary>
    public class AttributeBarViewTests
    {
        [Test]
        public void Evaluate_FillIsTheFractionOfMax_Clamped()
        {
            Assert.That(AttributeBarView.Evaluate(25f, 100f, false).Fill, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(AttributeBarView.Evaluate(-5f, 100f, false).Fill, Is.EqualTo(0f));
            Assert.That(AttributeBarView.Evaluate(150f, 100f, false).Fill, Is.EqualTo(1f));
        }

        [Test]
        public void Evaluate_HiddenWhenFull_UnlessShowWhenFull()
        {
            Assert.That(AttributeBarView.Evaluate(100f, 100f, false).Visible, Is.False, "a healthy part shows no bar");
            Assert.That(AttributeBarView.Evaluate(100f, 100f, true).Visible, Is.True);
            Assert.That(AttributeBarView.Evaluate(99f, 100f, false).Visible, Is.True, "any damage shows the bar");
            Assert.That(AttributeBarView.Evaluate(0f, 100f, false).Visible, Is.True, "a broken part shows an empty bar");
        }

        [Test]
        public void Evaluate_NoMaxMeansEmpty()
        {
            var (visible, fill) = AttributeBarView.Evaluate(50f, 0f, false);
            Assert.That(fill, Is.EqualTo(0f));
            Assert.That(visible, Is.True);
        }
    }
}
