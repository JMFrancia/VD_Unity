using UnityEngine;
using VoidDay.Core.Model;

namespace VoidDay.Systems
{
    /// Maps between Core grid cells (structural col,row) and world-space positions (§4.1, §12.6). Lives in
    /// Systems so the Core boundary stays Unity-free (CLAUDE.md rule 3): a GridCoord never carries a Vector3.
    ///
    /// The grid is centered on the world origin, so a cell's CENTER lands on a half-cell offset — matching the
    /// scene-authored pre-placed stations (e.g. cell (9,15) → world (-0.5, 0, 0.5) at cellSize 1, 20x30).
    /// Reintroduced for M4 placement (the pivot deleted the original when placement became transform-authored;
    /// runtime placement needs the mapping back).
    public readonly struct GridProjection
    {
        readonly int _cols;
        readonly int _rows;
        readonly float _cellSize;

        public GridProjection(int cols, int rows, float cellSize)
        {
            _cols = cols;
            _rows = rows;
            _cellSize = cellSize;
        }

        /// Cell → world position of that cell's center, on the ground plane (y = 0).
        public Vector3 CellToWorld(GridCoord cell)
        {
            float x = (cell.Col + 0.5f - _cols * 0.5f) * _cellSize;
            float z = (cell.Row + 0.5f - _rows * 0.5f) * _cellSize;
            return new Vector3(x, 0f, z);
        }

        /// World position → the cell whose center is nearest (used to snap the placement ghost).
        public GridCoord WorldToCell(Vector3 world)
        {
            int col = Mathf.FloorToInt(world.x / _cellSize + _cols * 0.5f);
            int row = Mathf.FloorToInt(world.z / _cellSize + _rows * 0.5f);
            return new GridCoord(col, row);
        }
    }
}
