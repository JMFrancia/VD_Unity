using System;
using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The XP → level table (§9). An explicit ordered set of thresholds, never a formula — which level a
    /// given lifetime XP total buys is a lookup, and the whole curve is a designer surface.
    ///
    /// Also owns the two numbers the XP bar renders (progress into the current level, and that level's span),
    /// so the View displays a level's progress rather than re-deriving it (§12.1 — "the View does not compute
    /// the threshold, it reads it").
    public sealed class LevelCurve
    {
        private readonly IReadOnlyList<LevelModel> _levels;

        public LevelCurve(IReadOnlyList<LevelModel> levels)
        {
            if (levels == null || levels.Count == 0)
                throw new ArgumentException("A level curve needs at least one level", nameof(levels));
            _levels = levels;
        }

        public int MaxLevel => _levels.Count;

        public LevelModel At(int level)
        {
            if (level < 1 || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"Level curve holds 1..{MaxLevel}");
            return _levels[level - 1];
        }

        /// Lifetime XP required to BE this level. The level above the top of the curve is unreachable, so it
        /// costs infinitely much — that is what stops the level-up loop at the cap.
        public int XpForLevel(int level) => level > MaxLevel ? int.MaxValue : At(level).XpThreshold;

        public bool IsMaxLevel(int level) => level >= MaxLevel;

        /// XP banked into the current level — the filled part of the bar.
        public int XpIntoLevel(int level, int xpTotal) => xpTotal - XpForLevel(level);

        /// XP the current level spans — the bar's denominator. 0 at the cap, where the bar reads full.
        public int XpSpanOfLevel(int level) =>
            IsMaxLevel(level) ? 0 : XpForLevel(level + 1) - XpForLevel(level);
    }
}
