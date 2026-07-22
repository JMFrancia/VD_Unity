using System;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The single authority on finishing a timer early with gems (§13) — the currency's only sink.
    ///
    /// It owns the *pricing rule* and nothing else. The three timer owners stay entirely separate; each one
    /// exposes a read ("how long is left") and a write ("pull the end to now"), and this routes to the right
    /// pair. The completion itself always happens on the owner's next Tick, down its normal path, so a
    /// skipped job / build / refill fires exactly the events a natural one does.
    ///
    /// Pricing lives here and nowhere else — no View re-derives a cost.
    public sealed class TimeSkip
    {
        private readonly EventBus _bus;
        private readonly GemPurse _gems;
        private readonly JobSystem _jobs;
        private readonly BuildSystem _builds;
        private readonly OrderBoard _orders;
        private readonly float _secondsPerGem;
        private readonly int _minGemCost;

        public TimeSkip(EventBus bus, GemPurse gems, JobSystem jobs, BuildSystem builds, OrderBoard orders,
            float secondsPerGem, int minGemCost)
        {
            _bus = bus;
            _gems = gems;
            _jobs = jobs;
            _builds = builds;
            _orders = orders;
            _secondsPerGem = secondsPerGem;
            _minGemCost = minGemCost;
        }

        /// Is there a live timer here at all? A timer already at (or past) its end is not skippable — there
        /// is nothing left to buy, and its owner's next Tick will finish it for free.
        public bool CanSkip(TimerRef timer, double now) => SecondsRemaining(timer, now) > 0f;

        /// The price of skipping: one gem per secondsPerGem of remaining wait, rounded UP, never below the
        /// floor. Ceiling not round, so the last partial gem-worth of waiting is still paid for.
        public int CostFor(TimerRef timer, double now)
        {
            float remaining = SecondsRemaining(timer, now);
            if (remaining <= 0f)
                throw new InvalidOperationException($"No live timer at {timer} to price");
            return Math.Max(_minGemCost, (int)Math.Ceiling(remaining / (double)_secondsPerGem));
        }

        /// Charge the purse, then pull the timer's end to now. Fails loud on an unaffordable purse *before*
        /// touching the timer: the popup disables its Confirm button when gems < cost, so reaching here
        /// short is a contract breach, and a half-applied skip would be worse than a stack trace.
        public void Skip(TimerRef timer, double now)
        {
            int cost = CostFor(timer, now);
            _gems.Spend(cost);

            switch (timer.Kind)
            {
                case TimerKind.Job: _jobs.SkipHead(timer.StationId, now); break;
                case TimerKind.Construction: _builds.SkipSite(timer.StationId, now); break;
                case TimerKind.OrderRefill: _orders.SkipRefill(timer.Slot, now); break;
                default: throw new InvalidOperationException($"Unhandled timer kind {timer.Kind}");
            }

            _bus.Publish(new TimerSkipped(timer, cost));
        }

        /// Negative means "no live timer of this kind here" — each owner uses the same sentinel, which is
        /// what lets one pricing rule serve three unrelated timers.
        private float SecondsRemaining(TimerRef timer, double now) => timer.Kind switch
        {
            TimerKind.Job => _jobs.HeadSecondsRemaining(timer.StationId, now),
            TimerKind.Construction => _builds.SiteSecondsRemaining(timer.StationId, now),
            TimerKind.OrderRefill => _orders.OrderAt(timer.Slot) != null
                ? -1f                                            // a filled slot has no refill running
                : _orders.RefillRemaining(timer.Slot, now),
            _ => throw new InvalidOperationException($"Unhandled timer kind {timer.Kind}")
        };
    }
}
