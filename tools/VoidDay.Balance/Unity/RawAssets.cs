namespace VoidDay.Balance.Unity;

// Raw DTOs matching the camelCase field names Unity serializes into .asset bodies. These are the
// *authoring* shapes; the reader projects them into Schema/BalanceConfig with enums as names and
// references resolved to ids. Unity writes bools as 0/1, so those fields are read as int here.
//
// Fields absent from an asset (added to the SO after the asset was last saved) keep the default
// assigned below, which mirrors the SO's field initializer — exactly the value Unity applies when
// it loads such an asset. This is faithful reading, not silent default-filling: a missing object
// *reference* still throws loud in GuidIndex.

public sealed class RawRef { public string? guid { get; set; } }

public sealed class GameConfigRaw
{
    public int gridCols { get; set; }
    public int gridRows { get; set; }
    public float cellSize { get; set; }
    public List<RawRef> stationRoster { get; set; } = new();
    public float refundPercent { get; set; }
    public int startingStorageCapacity { get; set; }
    public List<StartingResourceRaw> startingResources { get; set; } = new();
    public int startingGems { get; set; }
    public float secondsPerGem { get; set; }
    public int minGemCost { get; set; }
    public RawRef? orderConfig { get; set; }
    public RawRef? xpConfig { get; set; }
    public RawRef? levels { get; set; }
    public List<RawRef> quests { get; set; } = new();
}

public sealed class StartingResourceRaw
{
    public RawRef? resource { get; set; }
    public int amount { get; set; }
}

public sealed class StationRaw
{
    public string stationType { get; set; } = "";
    public string displayName { get; set; } = "";
    public int width { get; set; } = 1;
    public int height { get; set; } = 1;
    public int buildable { get; set; }              // Unity bool 0/1
    public int buildCost { get; set; }
    public int cap { get; set; } = 1;
    public float buildSeconds { get; set; } = 15f;  // absent in current assets → SO initializer
    public int unlockLevel { get; set; } = 1;
    public int queueDepth { get; set; } = 3;
    public RawRef? prefab { get; set; }
    public List<RawRef> recipes { get; set; } = new();
    public List<RawRef> upgrades { get; set; } = new();
}

public sealed class RecipeRaw
{
    public string id { get; set; } = "";
    public string stationType { get; set; } = "";
    public List<IngredientRaw> inputs { get; set; } = new();
    public List<IngredientRaw> outputs { get; set; } = new();
    public float duration { get; set; }
    public int unlockLevel { get; set; } = 1;   // absent in pre-Feature-A assets → SO initializer (unlock with station)
}

public sealed class IngredientRaw
{
    public RawRef? resource { get; set; }
    public int amount { get; set; } = 1;
}

public sealed class ResourceRaw
{
    public string id { get; set; } = "";
    public string displayName { get; set; } = "";
    public int baseValue { get; set; } = 1;
    public int sellable { get; set; }               // Unity bool 0/1
    public int tier { get; set; } = 1;
}

public sealed class UpgradeRaw
{
    public string id { get; set; } = "";
    public string displayName { get; set; } = "";
    public int unlockLevel { get; set; } = 1;
    public List<TierRaw> tiers { get; set; } = new();
}

public sealed class TierRaw
{
    public int cost { get; set; }
    public List<EffectRaw> effects { get; set; } = new();
}

public sealed class EffectRaw
{
    public string? id { get; set; }
    public int type { get; set; }
    public ValueRaw value { get; set; } = new();
    public string? resource { get; set; }
    public int range { get; set; }
    public int trigger { get; set; }
    public int triggerChance { get; set; }
    public ConditionRaw condition { get; set; } = new();
}

public sealed class ValueRaw
{
    public int op { get; set; }
    public float amount { get; set; }
}

public sealed class ConditionRaw
{
    public int type { get; set; }
    public string? arg { get; set; }
    public int amount { get; set; }
}

public sealed class OrderConfigRaw
{
    public int slotCount { get; set; }
    public float refillSeconds { get; set; }
    public int minRequestKinds { get; set; }
    public int maxRequestKinds { get; set; }
    public float maxQuantityAtLevel1 { get; set; }
    public float maxQuantityPerLevel { get; set; }
    public float tierWeightBase { get; set; }
    public float tierWeightPerLevel { get; set; }
    public float cashMultiplier { get; set; }
    public float xpMultiplier { get; set; }
}

public sealed class XpConfigRaw
{
    public int perJobCollected { get; set; } = 2;
    public int perStationBuilt { get; set; } = 5;   // absent in current asset → SO initializer
}

public sealed class LevelsRaw
{
    public List<LevelDefRaw> levels { get; set; } = new();
}

public sealed class LevelDefRaw
{
    public int xpThreshold { get; set; }
    public List<LevelGrantRaw> grants { get; set; } = new();
}

public sealed class LevelGrantRaw
{
    public int kind { get; set; }
    public RawRef? targetStation { get; set; }
    public int amount { get; set; }
}

// QuestSO body. Enums are ints on the asset (ConditionKind / GoalKind by index) → mapped to names in the
// reader; a reward resource is a ResourceSO reference resolved to its id, mirroring StartingResourceRaw.
public sealed class QuestRaw
{
    public string id { get; set; } = "";
    public List<QuestConditionRaw> conditions { get; set; } = new();
    public QuestGoalRaw goal { get; set; } = new();
    public QuestRewardRaw reward { get; set; } = new();
}

public sealed class QuestConditionRaw
{
    public int kind { get; set; }
    public int amount { get; set; }
    public string? arg { get; set; }
}

public sealed class QuestGoalRaw
{
    public int kind { get; set; }
    public int amount { get; set; }
    public string? targetId { get; set; }
}

public sealed class QuestRewardRaw
{
    public int xp { get; set; }
    public int money { get; set; }
    public int gems { get; set; }
    public List<QuestResourceGrantRaw> resources { get; set; } = new();
}

public sealed class QuestResourceGrantRaw
{
    public RawRef? resource { get; set; }
    public int amount { get; set; }
}
