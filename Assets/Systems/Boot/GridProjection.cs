using UnityEngine;
using VoidDay.Core.Model;

namespace VoidDay.Systems
{
    /// Projects a Core grid cell (col,row) to a world-space position on the XZ plane.
    /// Lives in the Systems layer because it produces a Vector3 — Core never touches Unity types.
    /// The grid is centered on the world origin so the ground plane sits symmetrically at (0,0,0).
    public static class GridProjection
    {
        public static Vector3 CellToWorld(GridCoord cell, GameConfigModel config)
        {
            float x = (cell.Col - (config.GridCols - 1) * 0.5f) * config.CellSize;
            float z = (cell.Row - (config.GridRows - 1) * 0.5f) * config.CellSize;
            return new Vector3(x, 0f, z);
        }
    }
}
