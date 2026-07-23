using System.Collections.Generic;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.Systems
{
    /// Projects Data-layer SOs into pure Core/Model objects at boot (§14). A Core rule reads a *Model,
    /// never a *SO. Asset refs (mesh/material) are dropped here — they stay on the SO for the View layer.
    public static class ModelProjector
    {
        public static GameConfigModel Project(GameConfigSO so) =>
            new GameConfigModel(so.gridCols, so.gridRows, so.cellSize);

        public static ResourceModel Project(ResourceSO so) =>
            new ResourceModel(so.id, so.displayName, so.baseValue, so.sellable, so.tier);

        public static OrderConfigModel Project(OrderConfigSO so) =>
            new OrderConfigModel(so.slotCount, so.refillSeconds, so.minRequestKinds, so.maxRequestKinds,
                so.maxQuantityAtLevel1, so.maxQuantityPerLevel, so.cashMultiplier, so.xpMultiplier,
                so.tierWeightBase, so.tierWeightPerLevel);

        public static XpConfigModel Project(XpConfigSO so) =>
            new XpConfigModel(so.perJobCollected, so.perStationBuilt);

        public static StationModel Project(StationSO so, string instanceId) =>
            new StationModel(instanceId, so.stationType, so.displayName, so.width, so.height);

        public static StationTypeModel ProjectType(StationSO so) =>
            new StationTypeModel(so.stationType, so.displayName, so.buildCost, so.cap, so.unlockLevel,
                so.queueDepth, so.width, so.height, so.buildSeconds);

        /// Upgrade track → model. Effects cross unprojected (§14) — the Core Effect[] is authored on the SO and
        /// handed to the resolver straight, no DTO.
        public static UpgradeTrackModel ProjectUpgrade(UpgradeSO so)
        {
            var tiers = new UpgradeTierModel[so.tiers.Length];
            for (int i = 0; i < so.tiers.Length; i++)
                tiers[i] = new UpgradeTierModel(so.tiers[i].cost, so.tiers[i].effects);
            return new UpgradeTrackModel(so.id, so.displayName, so.unlockLevel, tiers);
        }

        /// Level curve → Core (§9). A level's number is its position in the list; a grant's target resolves
        /// from the StationSO reference to the station type Core speaks, and an unassigned target means
        /// "every station type" (LevelGrants.AllTargets).
        public static LevelCurve ProjectLevels(LevelSO so)
        {
            var levels = new List<LevelModel>(so.levels.Count);
            for (int i = 0; i < so.levels.Count; i++)
            {
                var def = so.levels[i];
                var grants = new List<LevelGrantModel>(def.grants.Count);
                foreach (var g in def.grants)
                    grants.Add(new LevelGrantModel(g.kind,
                        g.targetStation != null ? g.targetStation.stationType : LevelGrants.AllTargets,
                        g.targetStation != null ? g.targetStation.displayName : "",
                        g.amount));
                levels.Add(new LevelModel(i + 1, def.xpThreshold, grants));
            }
            return new LevelCurve(levels);
        }

        /// Everything gated behind a level by its own asset (§9): station types, upgrade tracks, and recipes.
        /// A track or recipe reachable from more than one station appears once — the gate is the asset's, not
        /// the building's.
        public static IReadOnlyList<LevelUnlockModel> ProjectLevelGates(IReadOnlyList<StationSO> roster)
        {
            var gates = new List<LevelUnlockModel>();
            var seenTracks = new HashSet<string>();
            var seenRecipes = new HashSet<string>();
            foreach (var station in roster)
            {
                if (station.unlockLevel > Progression.StartingLevel)
                    gates.Add(new LevelUnlockModel(LevelEntryKind.StationType, station.stationType,
                        station.displayName, station.unlockLevel));

                foreach (var upgrade in station.upgrades)
                    if (upgrade.unlockLevel > Progression.StartingLevel && seenTracks.Add(upgrade.id))
                        gates.Add(new LevelUnlockModel(LevelEntryKind.Upgrade, upgrade.id,
                            upgrade.displayName, upgrade.unlockLevel));

                foreach (var recipe in station.recipes)
                    if (recipe.unlockLevel > Progression.StartingLevel && seenRecipes.Add(recipe.id))
                        gates.Add(new LevelUnlockModel(LevelEntryKind.Recipe, recipe.id,
                            RecipeLabel(recipe), recipe.unlockLevel));
            }
            return gates;
        }

        /// Player-facing name of a recipe for the level-up line: its output good, "Fallow"-prefixed when it has
        /// no inputs — the same wording the station panel uses on the tile.
        static string RecipeLabel(RecipeSO so)
        {
            string output = so.outputs.Count > 0 ? so.outputs[0].resource.displayName : so.id;
            return so.inputs.Count == 0 ? $"Fallow {output}" : output;
        }

        /// Quest → Core model (§ quest system). Conditions and goal carry across as plain (kind, amount, arg)
        /// rows; reward resource grants drop their ResourceSO handle for the resource id Core speaks.
        public static QuestModel ProjectQuest(QuestSO so)
        {
            var conditions = new List<QuestConditionModel>(so.conditions.Count);
            foreach (var c in so.conditions)
                conditions.Add(new QuestConditionModel(c.kind, c.amount, c.arg));

            var goal = new QuestGoalModel(so.goal.kind, so.goal.amount, so.goal.targetId);

            var resources = new List<ResourceAmount>();
            if (so.reward.resources != null)
                foreach (var g in so.reward.resources)
                    resources.Add(new ResourceAmount(g.resource.id, g.amount));
            var reward = new QuestRewardModel(so.reward.xp, so.reward.money, so.reward.gems, resources);

            return new QuestModel(so.id, conditions, goal, reward);
        }

        public static RecipeModel Project(RecipeSO so) =>
            new RecipeModel(so.id, so.stationType, Flatten(so.inputs), Flatten(so.outputs), so.duration,
                so.unlockLevel);

        // Drop the SO handle, keep the rule-relevant (id, amount) — Core speaks resource ids, not assets.
        static IReadOnlyList<ResourceAmount> Flatten(List<Ingredient> ingredients)
        {
            var list = new List<ResourceAmount>(ingredients.Count);
            foreach (var ing in ingredients)
                list.Add(new ResourceAmount(ing.resource.id, ing.amount));
            return list;
        }
    }
}
