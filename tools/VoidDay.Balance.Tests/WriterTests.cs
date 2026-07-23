using Newtonsoft.Json;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Unity;
using Xunit;

namespace VoidDay.Balance.Tests;

/// M02 guards for the writer. Every test here uses Plan() only — Plan validates and diffs but writes
/// nothing, so the tests never touch a real asset. Applying to disk is exercised by the CLI How-to-Test
/// steps, which revert with `git checkout`; a unit test must not mutate the tracked project.
public sealed class WriterTests
{
    private static (string root, EconomyReader reader, BalanceConfig current) ReadReal()
    {
        var root = FindProjectRoot();
        var reader = new EconomyReader(new AssetReader(new GuidIndex(root)));
        return (root, reader, reader.Read());
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && File.Exists(Path.Combine(dir.FullName, ".gitignore")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the Unity project root above the test binary.");
    }

    // A JSON round-trip of the just-read economy plans zero changes — the "second write reports zero
    // changes / byte-identical" invariant from the spec, proven without touching disk.
    [Fact]
    public void RoundTripPlansNoChanges()
    {
        var (root, reader, current) = ReadReal();
        var json = JsonConvert.SerializeObject(current);
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(json)!;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.True(plan.IsEmpty, "an unedited round-trip must plan zero changes");
    }

    // A single scalar edit is planned as exactly one change with the right old/new, and nothing else.
    [Fact]
    public void ScalarEditIsOneChange()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Recipes.Single(r => r.Id == "field.wheatGrow").Duration = 2.5f;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.RecipeInsertions);
        var change = Assert.Single(plan.Scalars);
        Assert.EndsWith("Recipe_Field_WheatGrow.asset", change.AssetPath);
        Assert.Equal("duration", change.Field);
        Assert.Equal("5", change.Old);
        Assert.Equal("2.5", change.New);
    }

    // A new recipe is planned as one structural insertion, not a scalar edit.
    [Fact]
    public void NewRecipeIsAnInsertion()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Recipes.Add(new RecipeConfig
        {
            Id = "field.unitTestGrow",
            StationType = "field",
            Inputs = new() { new ResourceQuantity { Resource = "wheat", Amount = 1 } },
            Outputs = new() { new ResourceQuantity { Resource = "wheat", Amount = 3 } },
            Duration = 4f
        });

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.Scalars);
        Assert.Equal("field.unitTestGrow", Assert.Single(plan.RecipeInsertions).Recipe.Id);
    }

    [Fact]
    public void SchemaVersionMismatchRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.SchemaVersion = 999;

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void BogusResourceIdRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Resources[0].Id = "NONEXISTENT_RESOURCE";

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("NONEXISTENT_RESOURCE", ex.Message);
    }

    // Feature B: a level xpThreshold edit is now a planned, positional level edit — not a refusal, not a scalar.
    [Fact]
    public void LevelXpThresholdEditIsOneLevelEdit()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        int oldValue = incoming.Levels[2].XpThreshold;
        incoming.Levels[2].XpThreshold = oldValue + 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.Scalars);
        var edit = Assert.Single(plan.LevelEdits);
        Assert.Equal(2, edit.LevelIndex);
        Assert.Null(edit.GrantIndex);
        Assert.Equal(oldValue.ToString(), edit.Old);
        Assert.Equal((oldValue + 1).ToString(), edit.New);
    }

    // A grant amount edit is a positional level edit that carries the grant index.
    [Fact]
    public void GrantAmountEditCarriesGrantIndex()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        // Level 2 (index 1) has grants in the baseline; bump the first grant's amount.
        int oldAmount = incoming.Levels[1].Grants[0].Amount;
        incoming.Levels[1].Grants[0].Amount = oldAmount + 5;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var edit = Assert.Single(plan.LevelEdits);
        Assert.Equal(1, edit.LevelIndex);
        Assert.Equal(0, edit.GrantIndex);
    }

    // Structural level surgery still has no surgical path — a grant's kind change is refused loud.
    [Fact]
    public void GrantKindChangeRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Levels[1].Grants[0].Kind = "Gems"; // was something else; retargeting a grant is structural

        var writer = new AssetWriter(root, reader, current);
        Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
    }

    // Feature B: a recipe unlockLevel edit is a single scalar change on that recipe's asset.
    [Fact]
    public void RecipeUnlockLevelEditIsOneScalar()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var recipe = incoming.Recipes.First();
        recipe.UnlockLevel += 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var change = Assert.Single(plan.Scalars);
        Assert.Equal("unlockLevel", change.Field);
    }

    // Feature B: an upgrade unlockLevel edit is a single scalar change on that upgrade's asset.
    [Fact]
    public void UpgradeUnlockLevelEditIsOneScalar()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var upgrade = incoming.Upgrades.First();
        upgrade.UnlockLevel += 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var change = Assert.Single(plan.Scalars);
        Assert.Equal("unlockLevel", change.Field);
    }
}
