namespace VoidDay.Balance.Schema;

/// The whole game economy as one serializable document. Every later verb (write, simulate,
/// eval, suggest) reads from an instance of this. Enums are always strings here — the reader
/// maps them through the real Core enum types on the way in, so the JSON never carries an int
/// discriminator that could silently reassign if an enum were reordered.
public sealed class BalanceConfig
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion = CurrentSchemaVersion;   // the writer (M2) refuses a mismatch
    public string Name = "baseline";

    public GlobalConfig Global = new();
    public XpConfig Xp = new();
    public GemConfig Gems = new();
    public OrderConfig Orders = new();
    public List<ResourceConfig> Resources = new();
    public List<RecipeConfig> Recipes = new();
    public List<StationConfig> Stations = new();
    public List<UpgradeConfig> Upgrades = new();
    public List<LevelConfig> Levels = new();   // index 0 == level 1
}

public sealed class GlobalConfig
{
    public int GridCols;
    public int GridRows;
    public float CellSize;
    public float RefundPercent;
    public int StartingStorageCapacity;
    public List<ResourceQuantity> StartingResources = new();
    public List<StartingStation> StartingStations = new();   // scanned from Farm.unity
}

public sealed class StartingStation
{
    public string StationType = "";
    public int Count;
}

public sealed class ResourceQuantity
{
    public string Resource = "";   // resource id
    public int Amount;
}

public sealed class XpConfig
{
    public int PerJobCollected;
    public int PerStationBuilt;
}

public sealed class GemConfig
{
    public int StartingGems;
    public float SecondsPerGem;
    public int MinGemCost;
}

public sealed class OrderConfig
{
    public int SlotCount;
    public float RefillSeconds;
    public int MinRequestKinds;
    public int MaxRequestKinds;
    public float MaxQuantityAtLevel1;
    public float MaxQuantityPerLevel;
    public float TierWeightBase;
    public float TierWeightPerLevel;
    public float CashMultiplier;
    public float XpMultiplier;
}

public sealed class ResourceConfig
{
    public string Id = "";
    public string DisplayName = "";
    public int BaseValue;
    public bool Sellable;
    public int Tier;
}

public sealed class RecipeConfig
{
    public string Id = "";
    public string StationType = "";
    public List<ResourceQuantity> Inputs = new();
    public List<ResourceQuantity> Outputs = new();
    public float Duration;
}

public sealed class StationConfig
{
    public string StationType = "";
    public string DisplayName = "";
    public bool Buildable;
    public int BuildCost;
    public int Cap;
    public int UnlockLevel;
    public int QueueDepth;
    public int Width;
    public int Height;
    public float BuildSeconds;
    public List<string> RecipeIds = new();
    public List<string> UpgradeIds = new();
}

public sealed class UpgradeConfig
{
    public string Id = "";
    public string DisplayName = "";
    public int UnlockLevel;
    public List<UpgradeTierConfig> Tiers = new();
}

public sealed class UpgradeTierConfig
{
    public int Cost;
    public List<EffectConfig> Effects = new();
}

/// Effect with every enum expressed as its Core name. `Global*`/`OrderPayout`/etc. that have no
/// resolver teeth are still faithfully read — the reader parses whatever is authored (§ milestone).
public sealed class EffectConfig
{
    public string Id = "";
    public string Type = "";        // EffectType name
    public string Op = "";          // EffectOp name
    public float Amount;
    public string Resource = "";
    public int Range;
    public string Trigger = "";     // TriggerType name
    public int TriggerChance;
    public string ConditionType = "";   // ConditionType name
    public string ConditionArg = "";
    public int ConditionAmount;
}

public sealed class LevelConfig
{
    public int XpThreshold;
    public List<LevelGrantConfig> Grants = new();
}

public sealed class LevelGrantConfig
{
    public string Kind = "";              // LevelEntryKind name
    public string? TargetStation;         // stationType, or null = every station type
    public int Amount;
}
