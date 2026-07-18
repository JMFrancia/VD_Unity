using System;
using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// Lookup of every recipe, by id and by station type (§5.2). Built at boot from the projected models.
    /// The panel reads ForStationType to draw recipe rows; the JobSystem reads Get to resolve a queued job.
    public sealed class RecipeCatalog
    {
        private readonly Dictionary<string, RecipeModel> _byId = new();
        private readonly Dictionary<string, List<RecipeModel>> _byStationType = new();

        public void Add(RecipeModel recipe)
        {
            if (_byId.ContainsKey(recipe.Id))
                throw new InvalidOperationException($"Duplicate recipe id '{recipe.Id}'");
            _byId[recipe.Id] = recipe;
            if (!_byStationType.TryGetValue(recipe.StationType, out var list))
            {
                list = new List<RecipeModel>();
                _byStationType[recipe.StationType] = list;
            }
            list.Add(recipe);
        }

        public RecipeModel Get(string id) =>
            _byId.TryGetValue(id, out var r) ? r : throw new InvalidOperationException($"No recipe with id '{id}'");

        public IReadOnlyList<RecipeModel> ForStationType(string stationType) =>
            _byStationType.TryGetValue(stationType, out var l) ? l : Array.Empty<RecipeModel>();
    }
}
