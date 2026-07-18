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

        public RecipeModel(string id, string stationType,
            IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs, float duration)
        {
            Id = id;
            StationType = stationType;
            Inputs = inputs;
            Outputs = outputs;
            Duration = duration;
        }
    }
}
