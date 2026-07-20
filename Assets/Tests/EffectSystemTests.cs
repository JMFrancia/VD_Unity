using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// The Effect system is the spine (§3): one schema, one resolver, one description generator. Its stacking
    /// math and the three §3.6 reference sentences are exactly what CLAUDE.md says to cover — bugs here don't
    /// crash, they just make every upgrade/pet/event quietly wrong. Pure C#, no Unity.
    public sealed class EffectSystemTests
    {
        static Effect E(EffectType type, EffectOp op, float amount, string resource = "",
            TriggerType trigger = TriggerType.None, int chance = 0, Condition condition = default) =>
            new Effect
            {
                type = type,
                value = new EffectValue { op = op, amount = amount },
                resource = resource,
                trigger = trigger,
                triggerChance = chance,
                condition = condition
            };

        // ---- §3.5 stacking math ----

        [Test]
        public void FlatAmountsSum_ThenAdd()
        {
            var effects = new List<Effect> { E(EffectType.StationYield, EffectOp.Flat, 1),
                                             E(EffectType.StationYield, EffectOp.Flat, 2) };
            Assert.AreEqual(6f, EffectResolver.Apply(3f, effects), 1e-4f);
        }

        [Test]
        public void PercentsSum_AndApplyOnce_NotCompounded()
        {
            var effects = new List<Effect> { E(EffectType.StationSpeed, EffectOp.Pct, 25),
                                             E(EffectType.StationSpeed, EffectOp.Pct, 25) };
            // +25% and +25% = +50% => ×1.5, NOT ×1.5625 (§3.5).
            Assert.AreEqual(150f, EffectResolver.Apply(100f, effects), 1e-4f);
        }

        [Test]
        public void MultsMultiplyInSequence()
        {
            var effects = new List<Effect> { E(EffectType.StationYield, EffectOp.Mult, 2),
                                             E(EffectType.StationYield, EffectOp.Mult, 3) };
            Assert.AreEqual(12f, EffectResolver.Apply(2f, effects), 1e-4f);
        }

        [Test]
        public void MixedOps_ApplyInFlatThenPctThenMultOrder()
        {
            var effects = new List<Effect> { E(EffectType.StationYield, EffectOp.Flat, 5),
                                             E(EffectType.StationYield, EffectOp.Pct, 50),
                                             E(EffectType.StationYield, EffectOp.Mult, 2) };
            // (10 + 5) * 1.5 * 2 = 45
            Assert.AreEqual(45f, EffectResolver.Apply(10f, effects), 1e-4f);
        }

        [Test]
        public void NegativePercent_ReducesTheValue()
        {
            var effects = new List<Effect> { E(EffectType.StationCost, EffectOp.Pct, -15) };
            Assert.AreEqual(85f, EffectResolver.Apply(100f, effects), 1e-4f);
        }

        [Test]
        public void EmptyEffects_ReturnBaseValue() =>
            Assert.AreEqual(7f, EffectResolver.Apply(7f, new List<Effect>()), 1e-4f);

        // ---- The seam: speed is inverted into a shorter timer, and it stacks additively ----

        [Test]
        public void TwoSpeedUpgrades_ShortenTheTimerByFifty_NotByCompounding()
        {
            var (resolver, upgrades, wallet) = Rig(SpeedTrack());
            upgrades.Register("field#0", "field");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.speed"); // +25%
            upgrades.Purchase("field#0", "field.speed"); // +25% => +50% total

            float duration = resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext("field#0"));
            Assert.AreEqual(5f / 1.5f, duration, 1e-4f, "two +25% speed = ×1.5 speed = 5s / 1.5, not 5s / 1.5625");
        }

        [Test]
        public void YieldUpgrade_RaisesOutputQuantity()
        {
            var (resolver, upgrades, wallet) = Rig(YieldTrack());
            upgrades.Register("field#0", "field");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.yield"); // +1 flat

            float yield = resolver.Resolve(1f, ResolveKind.OutputQuantity, new ResolveContext("field#0", "corn"));
            Assert.AreEqual(2f, yield, 1e-4f);
        }

        [Test]
        public void UpgradesAreOwnStation_ADifferentInstanceIsUnaffected()
        {
            var (resolver, upgrades, wallet) = Rig(SpeedTrack());
            upgrades.Register("field#0", "field");
            upgrades.Register("field#1", "field");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.speed");

            Assert.AreEqual(4f, resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext("field#0")), 1e-4f);
            Assert.AreEqual(5f, resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext("field#1")), 1e-4f,
                "an upgrade on field#0 must not touch field#1 (§3.2 own-station scope)");
        }

        // ---- UpgradeSystem purchase rules ----

        [Test]
        public void Purchase_ChargesTheTierCost()
        {
            var (_, upgrades, wallet) = Rig(SpeedTrack());
            upgrades.Register("field#0", "field");
            wallet.Add(100);
            upgrades.Purchase("field#0", "field.speed"); // tier 0 costs 50
            Assert.AreEqual(50, wallet.Money);
            Assert.AreEqual(1, upgrades.TierOf("field#0", "field.speed"));
        }

        [Test]
        public void Purchase_WhenBroke_Throws()
        {
            var (_, upgrades, _) = Rig(SpeedTrack());
            upgrades.Register("field#0", "field");
            Assert.Throws<System.InvalidOperationException>(() => upgrades.Purchase("field#0", "field.speed"));
        }

        [Test]
        public void Purchase_PastTopTier_Throws()
        {
            var (_, upgrades, wallet) = Rig(SpeedTrack());
            upgrades.Register("field#0", "field");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.speed");
            upgrades.Purchase("field#0", "field.speed"); // now at max (2 tiers)
            Assert.Throws<System.InvalidOperationException>(() => upgrades.Purchase("field#0", "field.speed"));
        }

        [Test]
        public void ResetLevels_ClearsPurchasedTiers()
        {
            var (resolver, upgrades, wallet) = Rig(SpeedTrack());
            upgrades.Register("field#0", "field");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.speed");
            upgrades.ResetLevels();

            Assert.AreEqual(0, upgrades.TierOf("field#0", "field.speed"));
            Assert.AreEqual(5f, resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext("field#0")), 1e-4f);
        }

        // ---- §3.6 procedural descriptions — the three user reference sentences ----

        [Test]
        public void HardWorker_Description()
        {
            var trait = new Trait { name = "Hard Worker", effects = new[] { E(EffectType.StationSpeed, EffectOp.Pct, 25) } };
            Assert.AreEqual("+25% speed at its station.", TraitDescription.Describe(trait));
        }

        [Test]
        public void Thrifty_Description()
        {
            var trait = new Trait { name = "Thrifty", effects = new[] { E(EffectType.StationCost, EffectOp.Pct, -15) } };
            Assert.AreEqual("-15% recipe cost at its station.", TraitDescription.Describe(trait));
        }

        [Test]
        public void CowLover_Description()
        {
            var effect = E(EffectType.StationYield, EffectOp.Mult, 3, trigger: TriggerType.JobCompleted, chance: 20,
                condition: new Condition { type = ConditionType.AssignedTo, arg = "pasture" });
            var trait = new Trait { name = "Cow Lover", effects = new[] { effect } };
            Assert.AreEqual("When assigned to a pasture, 20% chance of ×3 yield on job completion.",
                TraitDescription.Describe(trait));
        }

        // ---- Rigs ----

        static UpgradeTrackModel SpeedTrack() => new UpgradeTrackModel("field.speed", new[]
        {
            new UpgradeTierModel(50, new[] { E(EffectType.StationSpeed, EffectOp.Pct, 25) }),
            new UpgradeTierModel(120, new[] { E(EffectType.StationSpeed, EffectOp.Pct, 25) })
        });

        static UpgradeTrackModel YieldTrack() => new UpgradeTrackModel("field.yield", new[]
        {
            new UpgradeTierModel(60, new[] { E(EffectType.StationYield, EffectOp.Flat, 1) })
        });

        static (ValueResolver, UpgradeSystem, Wallet) Rig(params UpgradeTrackModel[] tracks)
        {
            var bus = new EventBus();
            var wallet = new Wallet(bus);
            var byType = new Dictionary<string, IReadOnlyList<UpgradeTrackModel>> { ["field"] = tracks };
            var upgrades = new UpgradeSystem(bus, wallet, byType);
            var resolver = new ValueResolver();
            resolver.SetEffectSource(upgrades);
            return (resolver, upgrades, wallet);
        }
    }
}
