using System.Collections.Generic;

namespace VoidDay.Core.Model
{
    public enum JobState
    {
        Queued,   // inputs already consumed (queue-time consumption, §4.4); waiting behind the head
        Running,  // the head job, timer ticking toward EndTime
        Complete  // finished; output sits at the station and blocks the queue until collected (§4.4)
    }

    /// One entry in a station's job queue (§4.4). Timers are stored as absolute timestamps (§13) so
    /// offline progress is a later change, not a rewrite. ConsumedInputs is what was actually deducted at
    /// queue time (post-resolve), so a not-yet-started cancel refunds exactly that.
    public sealed class Job
    {
        public readonly string RecipeId;
        public JobState State;
        public double StartTime;
        public double EndTime;
        public IReadOnlyList<ResourceAmount> ConsumedInputs;
        public IReadOnlyList<ResourceAmount> Outputs;

        public Job(string recipeId, IReadOnlyList<ResourceAmount> consumedInputs)
        {
            RecipeId = recipeId;
            State = JobState.Queued;
            ConsumedInputs = consumedInputs;
        }
    }
}
