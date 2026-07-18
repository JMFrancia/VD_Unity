using UnityEngine;
using VoidDay.Data;

namespace VoidDay.Systems
{
    /// Validates SO content once, at boot, before anything runs (CLAUDE.md Data loading, §14).
    /// On failure it throws immediately, naming the asset + field. Never default-fills a missing value —
    /// a silent fallback turns a blank inspector field into a mystery bug an hour later.
    public static class BootValidator
    {
        public static void Validate(GameConfigSO config)
        {
            Require(config != null, null, "GameConfigSO", "boot config reference is not assigned on GameBoot");

            Require(config.gridCols > 0, config, nameof(config.gridCols), "must be > 0");
            Require(config.gridRows > 0, config, nameof(config.gridRows), "must be > 0");
            Require(config.cellSize > 0f, config, nameof(config.cellSize), "must be > 0");

            Require(config.cameraMinZoom > 0f, config, nameof(config.cameraMinZoom), "must be > 0");
            Require(config.cameraMaxZoom >= config.cameraMinZoom, config, nameof(config.cameraMaxZoom),
                "must be >= cameraMinZoom");
            Require(config.cameraStartZoom >= config.cameraMinZoom && config.cameraStartZoom <= config.cameraMaxZoom,
                config, nameof(config.cameraStartZoom), "must be within [cameraMinZoom, cameraMaxZoom]");
            Require(config.cameraDistance > 0f, config, nameof(config.cameraDistance), "must be > 0");

            Require(config.startingResources != null && config.startingResources.Count > 0,
                config, nameof(config.startingResources), "must have at least one entry");
            foreach (var sr in config.startingResources)
            {
                Require(sr.resource != null, config, nameof(config.startingResources), "contains a null resource ref");
                ValidateResource(sr.resource);
                Require(sr.amount >= 0, sr.resource, "starting amount", "must be >= 0");
            }

            Require(config.prePlacedStations != null && config.prePlacedStations.Count > 0,
                config, nameof(config.prePlacedStations), "must have at least one entry");
            foreach (var ps in config.prePlacedStations)
            {
                Require(ps.station != null, config, nameof(config.prePlacedStations), "contains a null station ref");
                ValidateStation(ps.station);
                Require(ps.col >= 0 && ps.col < config.gridCols, ps.station, "pre-placed col",
                    $"col {ps.col} is outside grid width {config.gridCols}");
                Require(ps.row >= 0 && ps.row < config.gridRows, ps.station, "pre-placed row",
                    $"row {ps.row} is outside grid height {config.gridRows}");
            }
        }

        static void ValidateResource(ResourceSO r)
        {
            Require(!string.IsNullOrWhiteSpace(r.id), r, nameof(r.id), "must not be empty");
            Require(!string.IsNullOrWhiteSpace(r.displayName), r, nameof(r.displayName), "must not be empty");
        }

        static void ValidateStation(StationSO s)
        {
            Require(!string.IsNullOrWhiteSpace(s.stationType), s, nameof(s.stationType), "must not be empty");
            Require(!string.IsNullOrWhiteSpace(s.displayName), s, nameof(s.displayName), "must not be empty");
            Require(s.width > 0, s, nameof(s.width), "must be > 0");
            Require(s.height > 0, s, nameof(s.height), "must be > 0");
            Require(s.queueDepth > 0, s, nameof(s.queueDepth), "must be > 0");
            Require(s.placeholderScale != Vector3.zero, s, nameof(s.placeholderScale), "must not be zero");

            Require(s.recipes != null, s, nameof(s.recipes), "must not be null");
            foreach (var r in s.recipes)
            {
                Require(r != null, s, nameof(s.recipes), "contains a null recipe ref");
                ValidateRecipe(r, s.stationType);
            }
        }

        static void ValidateRecipe(RecipeSO r, string ownerStationType)
        {
            Require(!string.IsNullOrWhiteSpace(r.id), r, nameof(r.id), "must not be empty");
            Require(r.stationType == ownerStationType, r, nameof(r.stationType),
                $"is '{r.stationType}' but the station referencing it is '{ownerStationType}'");
            Require(r.outputs != null && r.outputs.Count > 0, r, nameof(r.outputs), "must have at least one output");
            ValidateIngredients(r, r.inputs, nameof(r.inputs));
            ValidateIngredients(r, r.outputs, nameof(r.outputs));
        }

        static void ValidateIngredients(RecipeSO r, System.Collections.Generic.List<Ingredient> list, string field)
        {
            Require(list != null, r, field, "must not be null");
            foreach (var ing in list)
            {
                Require(ing.resource != null, r, field, "contains an ingredient with no resource assigned");
                ValidateResource(ing.resource);
                Require(ing.amount > 0, r, field, $"ingredient '{ing.resource.id}' amount must be > 0");
            }
        }

        static void Require(bool condition, Object asset, string field, string why)
        {
            if (condition) return;
            string name = asset != null ? asset.name : "<unassigned>";
            throw new System.InvalidOperationException($"[Boot validation] {name}.{field} {why}");
        }
    }
}
