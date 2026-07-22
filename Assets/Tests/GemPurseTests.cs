using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// The gem purse (§ gems currency). Pure C#, headless — exactly the economy core CLAUDE.md says to test,
    /// and the kind of bug that never crashes: it just makes the balance quietly wrong.
    public sealed class GemPurseTests
    {
        EventBus _bus;
        readonly List<GemsChanged> _changes = new();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _changes.Clear();
            _bus.Subscribe<GemsChanged>(e => _changes.Add(e));
        }

        [Test]
        public void IsBornHoldingTheStartingAmountAndAnnouncesNothing()
        {
            var purse = new GemPurse(_bus, 5);
            Assert.AreEqual(5, purse.Gems);
            Assert.IsEmpty(_changes); // construction is not a transaction
        }

        [Test]
        public void EmitCurrentAnnouncesTheBalanceWithAZeroDelta()
        {
            new GemPurse(_bus, 5).EmitCurrent();
            Assert.AreEqual(1, _changes.Count);
            Assert.AreEqual(0, _changes[0].Delta);
            Assert.AreEqual(5, _changes[0].Total);
        }

        [Test]
        public void AddMovesTheBalanceAndAnnouncesTheDelta()
        {
            var purse = new GemPurse(_bus, 5);
            purse.Add(3);
            Assert.AreEqual(8, purse.Gems);
            Assert.AreEqual(3, _changes[0].Delta);
            Assert.AreEqual(8, _changes[0].Total);
        }

        [Test]
        public void CanAffordIsInclusiveOfTheExactBalance()
        {
            var purse = new GemPurse(_bus, 5);
            Assert.IsTrue(purse.CanAfford(5));
            Assert.IsFalse(purse.CanAfford(6));
        }

        [Test]
        public void SpendDeductsAndAnnouncesANegativeDelta()
        {
            var purse = new GemPurse(_bus, 5);
            purse.Spend(2);
            Assert.AreEqual(3, purse.Gems);
            Assert.AreEqual(-2, _changes[0].Delta);
            Assert.AreEqual(3, _changes[0].Total);
        }

        [Test]
        public void SpendingMoreThanTheBalanceThrowsRatherThanClamping()
        {
            var purse = new GemPurse(_bus, 5);
            Assert.Throws<System.InvalidOperationException>(() => purse.Spend(6));
            Assert.AreEqual(5, purse.Gems); // and leaves the balance untouched
            Assert.IsEmpty(_changes);
        }

        [Test]
        public void ResetReturnsToTheStartingAmountFromAboveAndBelow()
        {
            var purse = new GemPurse(_bus, 5);
            purse.Add(7);
            purse.Reset(5);
            Assert.AreEqual(5, purse.Gems);

            purse.Spend(4);
            purse.Reset(5);
            Assert.AreEqual(5, purse.Gems);
        }

        [Test]
        public void ResetAtTheStartingAmountAnnouncesNothing()
        {
            var purse = new GemPurse(_bus, 5);
            purse.Reset(5);
            Assert.IsEmpty(_changes);
        }
    }
}
