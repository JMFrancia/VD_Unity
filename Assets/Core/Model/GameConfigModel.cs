namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of GameConfigSO (§14). Grid layout scalars only — camera zoom/pitch/pan
    /// are View concerns and stay on the SO, read directly by the View layer.
    public sealed class GameConfigModel
    {
        public readonly int GridCols;
        public readonly int GridRows;
        public readonly float CellSize;

        public GameConfigModel(int gridCols, int gridRows, float cellSize)
        {
            GridCols = gridCols;
            GridRows = gridRows;
            CellSize = cellSize;
        }
    }
}
