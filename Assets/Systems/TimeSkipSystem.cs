using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.Systems
{
    /// The System that drives the Core TimeSkip rule, and the gems' owning system. It holds no rule: it
    /// translates the confirmed intent into a Core call with an absolute timestamp (§13), and routes the two
    /// gem debug affordances (add / reset) the same way every other cheat routes — through the bus, into the
    /// domain object that owns the state.
    ///
    /// The gem cheat and the gem reset lived on Producer in M1 only because gems had no system of their own
    /// yet. They belong here now.
    public sealed class TimeSkipSystem : MonoBehaviour
    {
        EventBus _bus;
        TimeSkip _skip;
        GemPurse _gems;
        int _startingGems; // Reset needs it: unlike money, a run starts with gems in hand

        public void Init(EventBus bus, TimeSkip skip, GemPurse gems, int startingGems)
        {
            _bus = bus;
            _skip = skip;
            _gems = gems;
            _startingGems = startingGems;

            _bus.Subscribe<TimerSkipConfirmed>(OnSkipConfirmed);
            _bus.Subscribe<DebugAddGemsRequested>(OnDebugAddGems);
            _bus.Subscribe<DebugResetRequested>(OnDebugReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<TimerSkipConfirmed>(OnSkipConfirmed);
            _bus.Unsubscribe<DebugAddGemsRequested>(OnDebugAddGems);
            _bus.Unsubscribe<DebugResetRequested>(OnDebugReset);
        }

        void OnSkipConfirmed(TimerSkipConfirmed e) => _skip.Skip(e.Timer, Time.timeAsDouble);

        void OnDebugAddGems(DebugAddGemsRequested e) => _gems.Add(e.Amount);

        void OnDebugReset(DebugResetRequested _) => _gems.Reset(_startingGems);
    }
}
