namespace VoidDay.Core.Model
{
    /// A quest's grant gate — what must be true for the quest to be handed to the player (§ quest system).
    /// Flat enum discriminator, mirroring ConditionType on Effect.cs: the condition is a plain (kind, amount,
    /// arg) row, inspector-friendly and pure Core so the balance tool compiles the rules in for free.
    public enum ConditionKind
    {
        // ⚠ Serialized by INTEGER index on QuestSO assets. APPEND ONLY — inserting/reordering silently
        // reassigns every authored condition (same rule as LevelEntryKind / EffectType).
        MinLevel,          // amount = player level required
        UpgradePurchased,  // arg = upgrade track id (not yet evaluated — no example quest exercises it)
        ResourceAtLeast,   // arg = resource id, amount = count held
        QuestCompleted     // arg = prerequisite quest id — satisfied once that quest is COLLECTED
    }

    /// What the player must DO to fill a quest's progress bar. Keyed to events the game already emits so a
    /// goal is data, not a new code path (§ quest system). APPEND ONLY (serialized by integer index).
    public enum GoalKind
    {
        EarnMoney,        // MoneyChanged, positive deltas only
        FulfillOrders,    // OrderFulfilled
        HarvestCrops,     // JobCollected, outputs matching TargetId
        PurchaseUpgrades, // UpgradePurchased (declared vocabulary — no example quest exercises it yet)
        BuildStations,    // StationBuilt (declared vocabulary — no example quest exercises it yet)
        ReachLevel        // LevelUp
    }
}
