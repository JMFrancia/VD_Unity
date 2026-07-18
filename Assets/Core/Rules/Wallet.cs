using VoidDay.Core.Events;

namespace VoidDay.Core.Rules
{
    /// The player's money (§4.3). Born in M2 holding 0 — there is no cash source until orders (M3), so the
    /// HUD counter binds to money:changed from day one and M3 only adds income. EmitCurrent pushes the
    /// starting value so the HUD renders without a special first-frame path.
    public sealed class Wallet
    {
        private readonly EventBus _bus;
        public int Money { get; private set; }

        public Wallet(EventBus bus) => _bus = bus;

        public void Add(int delta)
        {
            Money += delta;
            _bus.Publish(new MoneyChanged(delta, Money));
        }

        public void EmitCurrent() => _bus.Publish(new MoneyChanged(0, Money));

        public void Reset()
        {
            if (Money != 0) Add(-Money);
        }
    }
}
