namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of a placed StationSO (§14). Asset refs (mesh/material) stay on the SO;
    /// this carries only scalars the rules touch. Id is per placed instance; StationType is the SO's type key.
    public sealed class StationModel
    {
        public readonly string Id;
        public readonly string StationType;
        public readonly string DisplayName;
        public readonly int Width;
        public readonly int Height;

        public StationModel(string id, string stationType, string displayName, int width, int height)
        {
            Id = id;
            StationType = stationType;
            DisplayName = displayName;
            Width = width;
            Height = height;
        }
    }
}
