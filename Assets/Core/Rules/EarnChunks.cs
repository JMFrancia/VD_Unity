using System;

namespace VoidDay.Core.Rules
{
    /// Splits an earned amount into the per-particle chunks a collection burst throws. Integer arithmetic
    /// that decides whether the displayed total is exact: every chunk must sum back to the amount, or the
    /// counter lands short and nothing ever crashes to say so.
    public static class EarnChunks
    {
        /// Returns min(amount, maxParticles) chunks summing to exactly `amount`. The remainder rides on the
        /// leading chunks, so the first particles to leave are the fattest.
        public static int[] Split(int amount, int maxParticles)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "EarnChunks.Split: an earn burst of nothing is a caller bug, not a state to clamp past");
            if (maxParticles <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxParticles), maxParticles,
                    "EarnChunks.Split: maxParticles must be at least 1");

            int count = Math.Min(amount, maxParticles);
            int size = amount / count;
            int remainder = amount % count;

            var chunks = new int[count];
            for (int i = 0; i < count; i++) chunks[i] = size + (i < remainder ? 1 : 0);
            return chunks;
        }
    }
}
