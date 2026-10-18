using System.Globalization;

namespace FFXIV.Export;

public sealed record ExportOptions(
    string OutDir,
    bool SkipSync,
    bool FixtureOnly,
    int BucketSize,
    int Parallelism,
    IReadOnlyList<int> OnlyRecipeIds,
    IReadOnlyList<string> OnlyJobs,
    IReadOnlyList<int> FixtureRecipeIds,
    IReadOnlyList<string> FixtureJobs,
    string? FixtureOut)
{
    public const string Usage = """
        FFXIV.Export --out <dir> [--skip-sync] [--bucket-size 100] [--parallelism 4]
                     [--fixture-recipes 1,2,3 --fixture-jobs CRP,MIN --fixture-out <file>]

          --out              Directory that receives the static data set (created if missing).
          --skip-sync        Export from the database as it is instead of running the XIVAPI sync first.
          --fixture-only     Write only the fixture (needs --fixture-recipes); skips the data set.
          --bucket-size      Ids per recipe/item file; 1 gives one file per entity.
          --parallelism      Concurrent buckets (each on its own DbContext).
          --recipes          Export only these recipes and the items they reach (a small data set
                             for end-to-end tests and local development).
          --jobs             With --recipes: also export the gear files of these jobs.
          --fixture-recipes  Also write the entity closure of these recipes for the parity tests.
          --fixture-jobs     Jobs whose equipment the fixture includes.
          --fixture-out      Path of the fixture JSON file.
        """;

    public static ExportOptions Parse(string[] args)
    {
        string? outDir = null;
        var skipSync = false;
        var fixtureOnly = false;
        var bucketSize = 100;
        var parallelism = Math.Clamp(Environment.ProcessorCount, 2, 8);
        var onlyRecipes = new List<int>();
        var onlyJobs = new List<string>();
        var fixtureRecipes = new List<int>();
        var fixtureJobs = new List<string>();
        string? fixtureOut = null;

        for (var i = 0; i < args.Length; i++)
        {
            string Next()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"{args[i]} needs a value.");
                return args[++i];
            }

            switch (args[i])
            {
                case "--out": outDir = Next(); break;
                case "--skip-sync": skipSync = true; break;
                case "--fixture-only": fixtureOnly = true; break;
                case "--bucket-size": bucketSize = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--parallelism": parallelism = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--recipes":
                    onlyRecipes.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse));
                    break;
                case "--jobs":
                    onlyJobs.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(j => j.ToUpperInvariant()));
                    break;
                case "--fixture-recipes":
                    fixtureRecipes.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse));
                    break;
                case "--fixture-jobs":
                    fixtureJobs.AddRange(Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(j => j.ToUpperInvariant()));
                    break;
                case "--fixture-out": fixtureOut = Next(); break;
                default: throw new ArgumentException($"Unknown argument {args[i]}.");
            }
        }

        if (outDir is null)
            throw new ArgumentException("--out is required.");
        if (bucketSize < 1)
            throw new ArgumentException("--bucket-size must be at least 1.");
        if (parallelism < 1)
            throw new ArgumentException("--parallelism must be at least 1.");
        if (fixtureRecipes.Count > 0 != (fixtureOut is not null))
            throw new ArgumentException("--fixture-recipes and --fixture-out go together.");
        if (fixtureOnly && fixtureOut is null)
            throw new ArgumentException("--fixture-only needs --fixture-recipes and --fixture-out.");

        if (onlyJobs.Count > 0 && onlyRecipes.Count == 0)
            throw new ArgumentException("--jobs needs --recipes.");

        return new ExportOptions(outDir, skipSync, fixtureOnly, bucketSize, parallelism, onlyRecipes, onlyJobs, fixtureRecipes, fixtureJobs, fixtureOut);
    }
}
