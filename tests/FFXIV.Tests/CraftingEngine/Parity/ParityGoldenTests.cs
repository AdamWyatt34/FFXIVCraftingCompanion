using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Tests.CraftingEngine.Solver;

namespace FFXIV.Tests.CraftingEngine.Parity;

/// <summary>
/// Pins the C# engine's behaviour as golden files the Rust crate replays (crates/xiv-craft/tests).
/// Run with FFXIV_PARITY_GENERATE=1 to rewrite the goldens in the source tree after an intended
/// engine change; otherwise each test regenerates in memory and fails on the first byte that
/// differs, so an engine change without regeneration cannot land silently.
/// </summary>
public class ParityGoldenTests
{
    private const string GenerateVariable = "FFXIV_PARITY_GENERATE";
    private const int RandomSampleCount = 64;
    private static readonly int[] RandomSeeds = [0, 1, 42, 12345, -7, int.MaxValue];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions JsonIndented = new(Json) { WriteIndented = true };

    public static IEnumerable<object[]> Benchmarks => BenchmarkLoader.GetBenchmarkMemberData();

    [Theory]
    [MemberData(nameof(Benchmarks))]
    public void Benchmark_golden_is_current(BenchmarkEntry benchmark)
    {
        var text = new StringBuilder();
        foreach (var scenario in ParityRotationSources.ForBenchmark(benchmark))
        {
            text.Append(JsonSerializer.Serialize(ParityRotationSources.Build(scenario), Json));
            text.Append('\n');
        }

        AssertOrWrite($"{benchmark.Id}.jsonl", text.ToString());
    }

    [Fact]
    public void DotNetRandom_golden_is_current()
    {
        var series = RandomSeeds.Select(seed =>
        {
            var random = new Random(seed);
            return new DotNetRandomSeries(seed, Enumerable.Range(0, RandomSampleCount).Select(_ => random.NextDouble()).ToList());
        }).ToList();

        AssertOrWrite("dotnet-random.json", JsonSerializer.Serialize(new DotNetRandomGolden(series), JsonIndented) + "\n");
    }

    [Fact]
    public void EngineUnits_golden_is_current()
    {
        var golden = new EngineUnitsGolden(HqCases().ToList(), MacroCases().ToList());
        AssertOrWrite("engine-units.json", JsonSerializer.Serialize(golden, JsonIndented) + "\n");
    }

    private static IEnumerable<HqGoldenCase> HqCases()
    {
        HqGoldenCase Case(string name, int maxQuality, int factor,
            (int item, int qty, bool hq)[] ingredients, (int item, int hqQty)[] inputs)
        {
            var ingredientInfos = ingredients.Select(i => new RecipeIngredientInfo(i.item, i.qty, i.hq)).ToList();
            var hqInputs = inputs.Select(i => new HQIngredientInput(i.item, i.hqQty)).ToList();
            var expected = HQCalculator.CalculateStartingQuality(maxQuality, factor, ingredientInfos, hqInputs);
            return new HqGoldenCase(name, maxQuality, factor,
                ingredients.Select(i => new HqGoldenIngredient(i.item, i.qty, i.hq)).ToList(),
                inputs.Select(i => new HqGoldenInput(i.item, i.hqQty)).ToList(),
                expected);
        }

        (int, int, bool)[] standard = [(1, 2, true), (2, 1, true), (3, 3, false)];

        yield return Case("all-hq", 12000, 75, standard, [(1, 2), (2, 1)]);
        yield return Case("half-hq", 12000, 75, standard, [(1, 1)]);
        yield return Case("no-hq-inputs", 12000, 75, standard, []);
        yield return Case("zero-factor", 12000, 0, standard, [(1, 2)]);
        yield return Case("zero-max-quality", 0, 75, standard, [(1, 2)]);
        yield return Case("no-hqable-ingredients", 12000, 75, [(3, 3, false)], [(3, 3)]);
        yield return Case("hq-quantity-clamped", 12000, 75, standard, [(1, 99), (2, 99)]);
        yield return Case("hq-item-not-in-recipe", 12000, 75, standard, [(42, 5)]);
        yield return Case("duplicate-hq-input-first-wins", 12000, 75, standard, [(1, 1), (1, 2)]);
        yield return Case("odd-rounding", 7777, 33, [(1, 3, true), (2, 4, true)], [(1, 2), (2, 1)]);
        yield return Case("ingredient-hq-flag-false-with-input", 5000, 50, [(1, 1, true), (2, 1, false)], [(1, 1), (2, 1)]);
    }

    private static IEnumerable<MacroGoldenCase> MacroCases()
    {
        int Id(string name) => CraftingActionRegistry.GetByName(name)!.Id;

        var mixed = new[] { "MuscleMemory", "Veneration", "Groundwork", "Groundwork", "Manipulation", "Innovation",
            "BasicTouch", "StandardTouch", "AdvancedTouch", "GreatStrides", "ByregotsBlessing", "MastersMend",
            "Veneration", "Groundwork", "CarefulSynthesis", "TricksOfTheTrade", "CarefulObservation" }
            .Select(Id).ToList();

        MacroGoldenCase Case(string name, List<int> ids, MacroOptions options) =>
            new(name, ids,
                new MacroGoldenOptions(options.LinesPerMacro, options.SoundEffect, options.CompleteSoundEffect, options.IncludeEcho),
                MacroExporter.ExportToMacro(ids, options));

        yield return Case("default-two-macros", mixed, new MacroOptions());
        yield return Case("exactly-fourteen", mixed.Take(14).ToList(), new MacroOptions());
        yield return Case("fifteen", mixed.Take(15).ToList(), new MacroOptions());
        yield return Case("five-per-macro-custom-sounds", mixed, new MacroOptions { LinesPerMacro = 5, SoundEffect = 3, CompleteSoundEffect = 4 });
        yield return Case("no-echo", mixed.Take(6).ToList(), new MacroOptions { IncludeEcho = false });
        yield return Case("empty", [], new MacroOptions());
        yield return Case("unknown-ids-skipped", [Id("BasicSynthesis"), 999999, Id("BasicTouch")], new MacroOptions());
        yield return Case("only-unknown", [999999], new MacroOptions());
    }

    private static void AssertOrWrite(string fileName, string text)
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);

        if (Environment.GetEnvironmentVariable(GenerateVariable) == "1")
        {
            var target = Path.Combine(SourceParityDirectory(), fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);
            return;
        }

        var committed = Path.Combine(AppContext.BaseDirectory, "CraftingEngine", "TestData", "parity", fileName);
        if (!File.Exists(committed))
            throw new Xunit.Sdk.XunitException($"Golden '{fileName}' is missing; run the tests with {GenerateVariable}=1 to create it.");

        var expected = File.ReadAllBytes(committed);
        if (expected.AsSpan().SequenceEqual(bytes)) return;

        throw new Xunit.Sdk.XunitException(
            $"Golden '{fileName}' differs from the current engine output at {DescribeFirstDifference(expected, bytes)}. " +
            $"If the engine change is intended, rerun with {GenerateVariable}=1 and commit the regenerated goldens.");
    }

    private static string DescribeFirstDifference(byte[] expected, byte[] actual)
    {
        var expectedLines = Encoding.UTF8.GetString(expected).Split('\n');
        var actualLines = Encoding.UTF8.GetString(actual).Split('\n');
        var count = Math.Max(expectedLines.Length, actualLines.Length);

        for (var i = 0; i < count; i++)
        {
            var e = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var a = i < actualLines.Length ? actualLines[i] : "<missing>";
            if (e == a) continue;

            var column = 0;
            while (column < e.Length && column < a.Length && e[column] == a[column]) column++;
            var context = a.Length > 0 ? a[..Math.Min(a.Length, 80)] : e[..Math.Min(e.Length, 80)];
            return $"line {i + 1}, column {column + 1} (line starts: {context})";
        }

        return "an unknown position";
    }

    private static string SourceParityDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FFXIV.Tests.csproj")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("Could not locate FFXIV.Tests.csproj above the test output directory.");

        return Path.Combine(dir.FullName, "CraftingEngine", "TestData", "parity");
    }
}
