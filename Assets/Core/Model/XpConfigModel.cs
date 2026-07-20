namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of XpConfigSO (§9, §14). XP per action. Order XP is not here — an order
    /// carries its own XP, derived from what it requests (§6) — so this covers the flat per-action awards.
    public sealed class XpConfigModel
    {
        public readonly int PerJobCollected;
        public readonly int PerStationBuilt;

        public XpConfigModel(int perJobCollected, int perStationBuilt)
        {
            PerJobCollected = perJobCollected;
            PerStationBuilt = perStationBuilt;
        }
    }
}
