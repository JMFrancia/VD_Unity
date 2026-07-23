using System;
using NUnit.Framework;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// The one piece of the collection-particle feature that is pure arithmetic — and the one that can be
    /// wrong without ever throwing. If the chunks do not sum back to the payout, the money counter simply
    /// settles on the wrong number and looks like a rounding quirk.
    public sealed class EarnChunksTests
    {
        [Test]
        public void ChunksAlwaysSumToTheAmount()
        {
            foreach (int max in new[] { 1, 3, 10, 50 })
                for (int amount = 1; amount <= 50; amount++)
                {
                    var chunks = EarnChunks.Split(amount, max);
                    int sum = 0;
                    foreach (int c in chunks) sum += c;
                    Assert.AreEqual(amount, sum, $"amount {amount}, maxParticles {max}");
                }
        }

        [Test]
        public void CountNeverExceedsMaxParticles()
        {
            foreach (int max in new[] { 1, 3, 10, 50 })
                for (int amount = 1; amount <= 50; amount++)
                    Assert.LessOrEqual(EarnChunks.Split(amount, max).Length, max,
                        $"amount {amount}, maxParticles {max}");
        }

        [Test]
        public void CountIsAmountWhenAmountIsSmallerThanTheCap()
        {
            var chunks = EarnChunks.Split(4, 10);
            Assert.AreEqual(4, chunks.Length);
            foreach (int c in chunks) Assert.AreEqual(1, c);
        }

        [Test]
        public void EveryChunkIsAtLeastOne()
        {
            foreach (int max in new[] { 1, 3, 10, 50 })
                for (int amount = 1; amount <= 50; amount++)
                    foreach (int c in EarnChunks.Split(amount, max))
                        Assert.GreaterOrEqual(c, 1, $"amount {amount}, maxParticles {max}");
        }

        [Test]
        public void RemainderLandsOnTheLeadingChunks()
        {
            // 23 over 10 particles = 2 each, remainder 3 → the first three carry 3.
            var chunks = EarnChunks.Split(23, 10);
            Assert.AreEqual(new[] { 3, 3, 3, 2, 2, 2, 2, 2, 2, 2 }, chunks);
        }

        [Test]
        public void ExactDivisionSpreadsEvenly()
        {
            Assert.AreEqual(new[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 }, EarnChunks.Split(50, 10));
        }

        [Test]
        public void ASingleParticleCarriesTheWholeAmount()
        {
            Assert.AreEqual(new[] { 37 }, EarnChunks.Split(37, 1));
        }

        [Test]
        public void ZeroAmountThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EarnChunks.Split(0, 10));
        }

        [Test]
        public void NegativeAmountThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EarnChunks.Split(-1, 10));
        }

        [Test]
        public void NonPositiveMaxParticlesThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EarnChunks.Split(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => EarnChunks.Split(10, -1));
        }
    }
}
