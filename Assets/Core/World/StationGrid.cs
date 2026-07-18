using System;
using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.World
{
    /// The authoritative map of which cell holds which station (§4.1). Pure C# — no Unity.
    /// Built to support runtime add/remove + occupancy queries from day one: M4 places stations
    /// into this same registry, so it never assumes a fixed roster (see 00-summary Gotchas).
    public sealed class StationGrid
    {
        public readonly int Cols;
        public readonly int Rows;

        private readonly Dictionary<GridCoord, StationModel> _cells = new();

        public StationGrid(int cols, int rows)
        {
            if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols), "Grid cols must be > 0");
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), "Grid rows must be > 0");
            Cols = cols;
            Rows = rows;
        }

        public bool InBounds(GridCoord cell) =>
            cell.Col >= 0 && cell.Col < Cols && cell.Row >= 0 && cell.Row < Rows;

        public bool IsOccupied(GridCoord cell) => _cells.ContainsKey(cell);

        public int Count => _cells.Count;

        /// Fail loud: out-of-bounds or an occupied cell is a bug, not a recoverable state.
        public void Add(GridCoord cell, StationModel station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            if (!InBounds(cell))
                throw new ArgumentOutOfRangeException(nameof(cell), $"Cell {cell} is outside grid {Cols}x{Rows}");
            if (_cells.TryGetValue(cell, out var existing))
                throw new InvalidOperationException($"Cell {cell} already holds station '{existing.Id}'");
            _cells[cell] = station;
        }

        public void Remove(GridCoord cell)
        {
            if (!_cells.Remove(cell))
                throw new InvalidOperationException($"No station at cell {cell} to remove");
        }

        public bool TryGet(GridCoord cell, out StationModel station) => _cells.TryGetValue(cell, out station);

        public IEnumerable<KeyValuePair<GridCoord, StationModel>> All => _cells;
    }
}
