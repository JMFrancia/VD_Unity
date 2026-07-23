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

    // A nested-collection edit the writer has no surgical path for is refused, never silently dropped.
    [Fact]
    public void NestedLevelEditRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Levels[2].XpThreshold += 1;

        var writer = new AssetWriter(root, reader, current);
        Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
    }
}
