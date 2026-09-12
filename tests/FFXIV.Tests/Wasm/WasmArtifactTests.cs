using System.Diagnostics;
using System.Text.Json;

namespace FFXIV.Tests.Wasm;

/// <summary>
/// The frontend ships a prebuilt <c>frontend/src/wasm/xiv_craft_bg.wasm</c> (no Rust toolchain on
/// Vercel or in frontend CI). <c>BUILD.json</c> beside it records the git tree hash of
/// <c>crates/</c> it was built from; this compares that hash with the checked-out tree, so an
/// engine or solver change that lands without <c>npm run wasm:build</c> fails backend CI, which
/// every engine change reaches through the parity goldens under <c>tests/</c>.
/// </summary>
public class WasmArtifactTests
{
    [Fact]
    public void Committed_wasm_was_built_from_the_checked_out_crates()
    {
        var root = FindRepoRoot();
        if (root is null)
            return; // Not running from a source checkout (e.g. published test binaries).

        var buildJson = Path.Combine(root, "frontend", "src", "wasm", "BUILD.json");
        File.Exists(buildJson).Should().BeTrue(
            "frontend/src/wasm/BUILD.json is written by `npm run wasm:build` and must be committed with the artifact");

        var checkedOut = GitTreeHash(root, "HEAD:crates");
        if (checkedOut is null)
            return; // git unavailable or not a git checkout; nothing to compare against.
        if (!CratesWorkingTreeIsClean(root))
            return; // Mid-edit on a developer machine; `npm run wasm:check` covers that state.

        using var doc = JsonDocument.Parse(File.ReadAllText(buildJson));
        var recorded = doc.RootElement.GetProperty("cratesTreeHash").GetString();

        recorded.Should().Be(checkedOut,
            "crates/ changed since the committed .wasm was built; run `npm run wasm:build` in frontend/ and commit src/wasm/");
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FFXIVCraftingCompanion.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static string? GitTreeHash(string root, string revision)
    {
        var output = Git(root, $"rev-parse {revision}");
        return output?.Length == 40 ? output : null;
    }

    private static bool CratesWorkingTreeIsClean(string root)
    {
        return Git(root, "status --porcelain -- crates") == string.Empty;
    }

    private static string? Git(string root, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
