namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of a StationSO's TYPE data (§4.2, §14) — what the build rules need to know
    /// about a buildable type, independent of any placed instance. Asset refs (prefab/color) stay on the SO
    /// for the View. Base cost/cap are read through the value seam at the call site (M6/M8 give them teeth).
    public sealed class StationTypeModel
    {
        public readonly string StationType;
        public readonly string DisplayName;
        public readonly int BuildCostBase;
        public readonly int CapBase;
        public readonly int UnlockLevel;
        public readonly int QueueDepthBase;
        public readonly int Width;
        public readonly int Height;

        public StationTypeModel(string stationType, string displayName, int buildCostBase, int capBase,
            int unlockLevel, int queueDepthBase, int width, int height)
        {
            StationType = stationType;
            DisplayName = displayName;
            BuildCostBase = buildCostBase;
            CapBase = capBase;
            UnlockLevel = unlockLevel;
            QueueDepthBase = queueDepthBase;
            Width = width;
            Height = height;
        }
    }
}
