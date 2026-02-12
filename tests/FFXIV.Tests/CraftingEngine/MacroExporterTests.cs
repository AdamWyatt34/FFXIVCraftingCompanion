using FluentAssertions;
using FFXIV.Domain.CraftingEngine;

namespace FFXIV.Tests.CraftingEngine;

public class MacroExporterTests
{
    // Action IDs
    private const int BasicSynthesis = 100001;
    private const int BasicTouch = 100002;
    private const int Veneration = 100297;
    private const int Innovation = 100209;
    private const int Manipulation = 100278;
    private const int GreatStrides = 100260;
    private const int Groundwork = 100403;
    private const int CarefulSynthesis = 100203;
    private const int MastersMend = 100003;

    [Fact]
    public void SingleMacro_FitsWithin14Actions()
    {
        var actionIds = new List<int> { BasicSynthesis, BasicTouch, Veneration };
        var macros = MacroExporter.ExportToMacro(actionIds);

        macros.Should().HaveCount(1);

        var lines = macros[0].Split('\n');
        lines.Should().HaveCount(4); // 3 actions + 1 echo
        lines[0].Should().Be("/ac \"Basic Synthesis\" <wait.3>");
        lines[1].Should().Be("/ac \"Basic Touch\" <wait.3>");
        lines[2].Should().Be("/ac \"Veneration\" <wait.2>");
        lines[3].Should().Be("/echo Craft complete <se.2>");
    }

    [Fact]
    public void MultiMacro_SplitsAt14Actions()
    {
        // 16 actions → 2 macros (14 + 2)
        var actionIds = new List<int>();
        for (int i = 0; i < 16; i++)
            actionIds.Add(BasicSynthesis);

        var macros = MacroExporter.ExportToMacro(actionIds);

        macros.Should().HaveCount(2);

        var firstLines = macros[0].Split('\n');
        firstLines.Should().HaveCount(15); // 14 actions + 1 echo
        firstLines.Last().Should().Contain("Macro #1 complete");

        var secondLines = macros[1].Split('\n');
        secondLines.Should().HaveCount(3); // 2 actions + 1 echo
        secondLines.Last().Should().Contain("Craft complete");
    }

    [Fact]
    public void BuffActions_GetWaitTime2()
    {
        var buffIds = new List<int> { Veneration, Innovation, Manipulation, GreatStrides };
        var macros = MacroExporter.ExportToMacro(buffIds);

        var lines = macros[0].Split('\n');
        lines[0].Should().Contain("<wait.2>");
        lines[1].Should().Contain("<wait.2>");
        lines[2].Should().Contain("<wait.2>");
        lines[3].Should().Contain("<wait.2>");
    }

    [Fact]
    public void NonBuffActions_GetWaitTime3()
    {
        var actionIds = new List<int> { BasicSynthesis, BasicTouch, Groundwork, MastersMend };
        var macros = MacroExporter.ExportToMacro(actionIds);

        var lines = macros[0].Split('\n');
        lines[0].Should().Contain("<wait.3>"); // Progress
        lines[1].Should().Contain("<wait.3>"); // Quality
        lines[2].Should().Contain("<wait.3>"); // Progress
        lines[3].Should().Contain("<wait.3>"); // Repair
    }

    [Fact]
    public void EchoLines_UseCorrectSoundEffects()
    {
        var actionIds = new List<int>();
        for (int i = 0; i < 20; i++)
            actionIds.Add(BasicSynthesis);

        var macros = MacroExporter.ExportToMacro(actionIds);

        // Intermediate macros: se.1
        macros[0].Should().Contain("<se.1>");
        // Last macro: se.2
        macros.Last().Should().Contain("<se.2>");
    }

    [Fact]
    public void CustomOptions_ApplyCorrectly()
    {
        var actionIds = new List<int>();
        for (int i = 0; i < 10; i++)
            actionIds.Add(BasicSynthesis);

        var options = new MacroOptions
        {
            LinesPerMacro = 5,
            SoundEffect = 4,
            CompleteSoundEffect = 9,
        };

        var macros = MacroExporter.ExportToMacro(actionIds, options);

        macros.Should().HaveCount(2); // 5 + 5

        var firstLines = macros[0].Split('\n');
        firstLines.Should().HaveCount(6); // 5 actions + 1 echo
        firstLines.Last().Should().Contain("<se.4>");

        var lastLines = macros[1].Split('\n');
        lastLines.Last().Should().Contain("<se.9>");
    }

    [Fact]
    public void IncludeEchoFalse_OmitsEchoLines()
    {
        var actionIds = new List<int> { BasicSynthesis, BasicTouch };
        var options = new MacroOptions { IncludeEcho = false };

        var macros = MacroExporter.ExportToMacro(actionIds, options);

        macros.Should().HaveCount(1);
        var lines = macros[0].Split('\n');
        lines.Should().HaveCount(2); // Only action lines, no echo
        lines.Should().AllSatisfy(l => l.Should().StartWith("/ac"));
    }

    [Fact]
    public void EmptyActionList_ReturnsEmptyList()
    {
        var macros = MacroExporter.ExportToMacro(new List<int>());
        macros.Should().BeEmpty();
    }

    [Fact]
    public void UnknownActions_AreSkipped()
    {
        var actionIds = new List<int> { 999999, BasicSynthesis, 888888 };
        var macros = MacroExporter.ExportToMacro(actionIds);

        macros.Should().HaveCount(1);
        var lines = macros[0].Split('\n');
        // Only BasicSynthesis should appear (+ echo)
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("Basic Synthesis");
    }

    [Fact]
    public void Exactly14Actions_ProducesSingleMacro()
    {
        var actionIds = new List<int>();
        for (int i = 0; i < 14; i++)
            actionIds.Add(BasicSynthesis);

        var macros = MacroExporter.ExportToMacro(actionIds);

        macros.Should().HaveCount(1);
        var lines = macros[0].Split('\n');
        lines.Should().HaveCount(15); // 14 + echo
        lines.Last().Should().Contain("Craft complete");
    }

    [Fact]
    public void ActionNames_UseInGameNames()
    {
        var actionIds = new List<int> { CarefulSynthesis, Groundwork };
        var macros = MacroExporter.ExportToMacro(actionIds);

        macros[0].Should().Contain("/ac \"Careful Synthesis\"");
        macros[0].Should().Contain("/ac \"Groundwork\"");
    }
}
