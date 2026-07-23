using System.Collections.Generic;

namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of a RecipeSO (§5.2, §14). A recipe converts inputs → outputs, optionally
    /// on a timer. Duration ≤ 0 means the job completes instantly (§5.2). Fallow recipes have empty inputs.
    /// The one generic Producer is driven by these — behaviour is data, never a per-station subclass (§4.1).
    public sealed class RecipeModel
    {
        public readonly string Id;
        public readonly string StationType;
        public readonly IReadOnlyList<ResourceAmount> Inputs;
        public readonly IReadOnlyList<ResourceAmount> Outputs;
        public readonly float Duration;

        /// Player level at which this recipe becomes queueable (§9). Gated like a station or upgrade track,
        /// off the recipe's own asset — the JobSystem refuses a queue below it, the panel shows it locked.
        public readonly int UnlockLevel;

        public RecipeModel(string id, string stationType,
            IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs, float duration,
            int unlockLevel)
        {
            Id = id;
            StationType = stationType;
            Inputs = inputs;
            Outputs = outputs;
            Duration = duration;
            UnlockLevel = unlockLevel;
        }
    }
}
