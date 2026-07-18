namespace VoidDay.Core.Model
{
    /// A cell on the station grid. Structural (col,row) only — never a world-space Vector3
    /// (CLAUDE.md rule 3: the Core boundary). The View layer projects a cell to a Vector3.
    public readonly struct GridCoord
    {
        public readonly int Col;
        public readonly int Row;

        public GridCoord(int col, int row)
        {
            Col = col;
            Row = row;
        }

        public override string ToString() => $"({Col},{Row})";
        public override bool Equals(object obj) => obj is GridCoord o && o.Col == Col && o.Row == Row;
        public override int GetHashCode() => (Col * 397) ^ Row;
    }
}
