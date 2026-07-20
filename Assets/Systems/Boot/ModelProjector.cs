using System.Collections.Generic;
using VoidDay.Core.Model;
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
                so.queueDepth, so.width, so.height);

        public static RecipeModel Project(RecipeSO so) =>
            new RecipeModel(so.id, so.stationType, Flatten(so.inputs), Flatten(so.outputs), so.duration);

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
