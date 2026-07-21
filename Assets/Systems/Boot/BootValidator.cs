using UnityEngine;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
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

            Require(config.startingResources != null && config.startingResources.Count > 0,
                config, nameof(config.startingResources), "must have at least one entry");
            foreach (var sr in config.startingResources)
            {
                Require(sr.resource != null, config, nameof(config.startingResources), "contains a null resource ref");
                ValidateResource(sr.resource);
                Require(sr.amount >= 0, sr.resource, "starting amount", "must be >= 0");
            }

            Require(config.orderConfig != null, config, nameof(config.orderConfig), "must be assigned");
            ValidateOrderConfig(config.orderConfig);
            Require(config.xpConfig != null, config, nameof(config.xpConfig), "must be assigned");
            Require(config.xpConfig.perJobCollected >= 0, config.xpConfig,
                nameof(config.xpConfig.perJobCollected), "must be >= 0");
            Require(config.xpConfig.perStationBuilt >= 0, config.xpConfig,
                nameof(config.xpConfig.perStationBuilt), "must be >= 0");

            Require(config.refundPercent >= 0f && config.refundPercent <= 1f, config,
                nameof(config.refundPercent), "must be within [0, 1]");
            Require(config.stationRoster != null && config.stationRoster.Count > 0, config,
                nameof(config.stationRoster), "must list every buildable station type (the build menu reads it)");
            foreach (var s in config.stationRoster)
            {
                Require(s != null, config, nameof(config.stationRoster), "contains a null station ref");
                ValidateStation(s);
            }
        }

        static void ValidateOrderConfig(OrderConfigSO o)
        {
            Require(o.slotCount > 0, o, nameof(o.slotCount), "must be > 0");
            Require(o.refillSeconds > 0f, o, nameof(o.refillSeconds), "must be > 0");
            Require(o.minRequestKinds > 0, o, nameof(o.minRequestKinds), "must be > 0");
            Require(o.maxRequestKinds >= o.minRequestKinds, o, nameof(o.maxRequestKinds),
                $"must be >= minRequestKinds ({o.minRequestKinds})");
            Require(o.maxQuantityAtLevel1 >= 1f, o, nameof(o.maxQuantityAtLevel1), "must be >= 1");
            Require(o.maxQuantityPerLevel >= 0f, o, nameof(o.maxQuantityPerLevel), "must be >= 0");
            Require(o.tierWeightBase > 0f, o, nameof(o.tierWeightBase),
                "must be > 0 — a zero base makes every level-1 weight zero and the pick undefined");
            Require(o.tierWeightPerLevel >= 0f, o, nameof(o.tierWeightPerLevel), "must be >= 0");
            Require(o.cashMultiplier > 0f, o, nameof(o.cashMultiplier), "must be > 0");
            Require(o.xpMultiplier > 0f, o, nameof(o.xpMultiplier), "must be > 0");
        }

        static void ValidateResource(ResourceSO r)
        {
            Require(!string.IsNullOrWhiteSpace(r.id), r, nameof(r.id), "must not be empty");
            Require(!string.IsNullOrWhiteSpace(r.displayName), r, nameof(r.displayName), "must not be empty");
            Require(r.icon != null, r, nameof(r.icon), "must be assigned — every resource renders an icon in rows, cards, and totals");
            Require(r.baseValue > 0, r, nameof(r.baseValue), "must be > 0 — it is the basis of order payout");
            Require(r.tier > 0, r, nameof(r.tier), "must be > 0");
            if (r is CropSO crop)
                Require(crop.cropSprite != null, crop, nameof(crop.cropSprite),
                    "must be assigned — the field renders this sprite rising out of the soil as the crop grows");
        }

        /// Called per scene-placed station at boot (GameBoot discovers them; the scene owns placement).
        public static void ValidateStation(StationSO s)
        {
            Require(!string.IsNullOrWhiteSpace(s.stationType), s, nameof(s.stationType), "must not be empty");
            Require(!string.IsNullOrWhiteSpace(s.displayName), s, nameof(s.displayName), "must not be empty");
            Require(s.width > 0, s, nameof(s.width), "must be > 0");
            Require(s.height > 0, s, nameof(s.height), "must be > 0");
            Require(s.queueDepth > 0, s, nameof(s.queueDepth), "must be > 0");
            Require(s.buildCost >= 0, s, nameof(s.buildCost), "must be >= 0");
            Require(s.cap > 0, s, nameof(s.cap), "must be > 0");
            Require(s.unlockLevel >= Progression.StartingLevel, s, nameof(s.unlockLevel),
                $"must be >= {Progression.StartingLevel} (the starting level)");
            Require(s.buildThumbnail != null, s, nameof(s.buildThumbnail),
                "must be assigned — every station type renders a thumbnail in the build menu");
            // A type buildable from the start must have a prefab to instantiate; level-locked types get theirs
            // when M8 makes them placeable (they only render as menu thumbnails until then).
            if (s.unlockLevel <= Progression.StartingLevel)
                Require(s.prefab != null, s, nameof(s.prefab),
                    "must be assigned for a type buildable at the starting level");

            Require(s.recipes != null, s, nameof(s.recipes), "must not be null");
            foreach (var r in s.recipes)
            {
                Require(r != null, s, nameof(s.recipes), "contains a null recipe ref");
                ValidateRecipe(r, s.stationType);
            }

            Require(s.upgrades != null, s, nameof(s.upgrades), "must not be null");
            foreach (var u in s.upgrades)
            {
                Require(u != null, s, nameof(s.upgrades), "contains a null upgrade ref");
                ValidateUpgrade(u);
            }
        }

        static void ValidateUpgrade(UpgradeSO u)
        {
            Require(!string.IsNullOrWhiteSpace(u.id), u, nameof(u.id), "must not be empty");
            Require(u.tiers != null && u.tiers.Length > 0, u, nameof(u.tiers), "must have at least one tier");
            for (int i = 0; i < u.tiers.Length; i++)
            {
                var tier = u.tiers[i];
                Require(tier.cost >= 0, u, $"{nameof(u.tiers)}[{i}].cost", "must be >= 0");
                Require(tier.effects != null && tier.effects.Length > 0, u, $"{nameof(u.tiers)}[{i}].effects",
                    "must grant at least one effect");
                foreach (var e in tier.effects)
                    ValidateEffect(u, e, $"{nameof(u.tiers)}[{i}]");
            }
        }

        /// §3.1 effect validation: reject malformed combinations loudly, and normalise triggerChance 0 → 100
        /// ("unset"). local.* / pet.* types require a range; those aren't resolved until M9/M10 but a designer
        /// authoring one now must not get a silent 0-cell reach.
        static void ValidateEffect(Object owner, Effect e, string where)
        {
            Require(e != null, owner, where, "contains a null effect");
            if (e.triggerChance == 0) e.triggerChance = 100; // normalise: unset => always fires
            Require(e.triggerChance >= 0 && e.triggerChance <= 100, owner, $"{where} triggerChance",
                "must be within [0, 100]");
            if (e.value.op == EffectOp.Mult)
                Require(e.value.amount > 0f, owner, $"{where} effect '{e.id}'",
                    "a Mult amount must be > 0 (a zero multiplier would erase the value)");
            if (RequiresRange(e.type))
                Require(e.range > 0, owner, $"{where} effect '{e.id}'",
                    $"type {e.type} is range-scoped and must set range > 0");
        }

        static bool RequiresRange(EffectType t) => t switch
        {
            EffectType.LocalSpeed or EffectType.LocalCost or EffectType.LocalYield
                or EffectType.PetEffectStrength or EffectType.PetAutoCollectSpeed => true,
            _ => false
        };

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
