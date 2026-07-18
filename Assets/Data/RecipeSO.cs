using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.Data
{
    /// One asset per recipe (§5.2, §14). Inputs/outputs reference ResourceSOs (the inspector authoring
    /// surface); projection flattens them to resource ids for Core. duration ≤ 0 ⇒ instant (§5.2).
    /// Fallow recipes simply have an empty inputs list. A recipe belongs to a station type, not an instance.
    [CreateAssetMenu(menuName = "VoidDay/Recipe", fileName = "Recipe")]
    public sealed class RecipeSO : ScriptableObject
    {
        public string id;
        public string stationType;

        [Tooltip("Empty = a free producer (Fallow, §5.2). Inputs are consumed at queue time (§4.4).")]
        public List<Ingredient> inputs = new();

        public List<Ingredient> outputs = new();

        [Tooltip("Seconds. ≤ 0 = instant (§5.2). 'fast' vs 'very slow' is just this number.")]
        public float duration = 5f;
    }

    [Serializable]
    public sealed class Ingredient
    {
        public ResourceSO resource;
        public int amount = 1;
    }
}
