using FluentAssertions;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class ConditionResolverTests
{
    [Fact]
    public void Excellent_AlwaysTransitionsToPoor()
    {
        var resolver = new ConditionResolver(seed: 42);
        var result = resolver.GetNextCondition(CraftCondition.Excellent, isExpert: false);
        result.Should().Be(CraftCondition.Poor);
    }

    [Fact]
    public void Good_AlwaysTransitionsToNormal()
    {
        var resolver = new ConditionResolver(seed: 42);
        var result = resolver.GetNextCondition(CraftCondition.Good, isExpert: false);
        result.Should().Be(CraftCondition.Normal);
    }

    [Fact]
    public void Poor_AlwaysTransitionsToNormal()
    {
        var resolver = new ConditionResolver(seed: 42);
        var result = resolver.GetNextCondition(CraftCondition.Poor, isExpert: false);
        result.Should().Be(CraftCondition.Normal);
    }

    [Fact]
    public void GoodOmen_AlwaysTransitionsToGood()
    {
        var resolver = new ConditionResolver(seed: 42);
        var result = resolver.GetNextCondition(CraftCondition.GoodOmen, isExpert: false);
        result.Should().Be(CraftCondition.Good);
    }

    [Fact]
    public void DeterministicTransitions_IgnoreExpertFlag()
    {
        var resolver = new ConditionResolver(seed: 42);

        resolver.GetNextCondition(CraftCondition.Excellent, isExpert: true).Should().Be(CraftCondition.Poor);
        resolver.GetNextCondition(CraftCondition.Good, isExpert: true).Should().Be(CraftCondition.Normal);
        resolver.GetNextCondition(CraftCondition.Poor, isExpert: true).Should().Be(CraftCondition.Normal);
        resolver.GetNextCondition(CraftCondition.GoodOmen, isExpert: true).Should().Be(CraftCondition.Good);
    }

    [Fact]
    public void StandardRecipe_ProducesExpectedDistribution()
    {
        var resolver = new ConditionResolver(seed: 12345);
        var counts = new Dictionary<CraftCondition, int>
        {
            [CraftCondition.Normal] = 0,
            [CraftCondition.Good] = 0,
            [CraftCondition.Excellent] = 0,
        };

        const int iterations = 10000;
        for (int i = 0; i < iterations; i++)
        {
            var result = resolver.GetNextCondition(CraftCondition.Normal, isExpert: false);
            counts[result]++;
        }

        // Expected: ~76% Normal, ~20% Good, ~4% Excellent (with tolerance)
        double normalPct = (double)counts[CraftCondition.Normal] / iterations * 100;
        double goodPct = (double)counts[CraftCondition.Good] / iterations * 100;
        double excellentPct = (double)counts[CraftCondition.Excellent] / iterations * 100;

        normalPct.Should().BeApproximately(76.0, 3.0);
        goodPct.Should().BeApproximately(20.0, 3.0);
        excellentPct.Should().BeApproximately(4.0, 2.0);
    }

    [Fact]
    public void StandardRecipe_SeededProducesReproducibleResults()
    {
        var resolver1 = new ConditionResolver(seed: 999);
        var resolver2 = new ConditionResolver(seed: 999);

        for (int i = 0; i < 100; i++)
        {
            var r1 = resolver1.GetNextCondition(CraftCondition.Normal, isExpert: false);
            var r2 = resolver2.GetNextCondition(CraftCondition.Normal, isExpert: false);
            r1.Should().Be(r2);
        }
    }

    [Fact]
    public void ExpertRecipe_OnlyProducesEnabledConditions()
    {
        // Enable Good (0x01) + Pliant (0x08) = 0x09
        int conditionsFlag = 0x09;
        var resolver = new ConditionResolver(seed: 42);
        var seen = new HashSet<CraftCondition>();

        for (int i = 0; i < 1000; i++)
        {
            var result = resolver.GetNextCondition(CraftCondition.Normal, isExpert: true, conditionsFlag);
            seen.Add(result);
        }

        seen.Should().BeSubsetOf(new[] { CraftCondition.Normal, CraftCondition.Good, CraftCondition.Pliant });
        seen.Should().Contain(CraftCondition.Normal);
        seen.Should().Contain(CraftCondition.Good);
        seen.Should().Contain(CraftCondition.Pliant);
    }

    [Fact]
    public void ExpertRecipe_NoFlags_AlwaysNormal()
    {
        var resolver = new ConditionResolver(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var result = resolver.GetNextCondition(CraftCondition.Normal, isExpert: true, conditionsFlag: 0);
            result.Should().Be(CraftCondition.Normal);
        }
    }

    [Fact]
    public void ExpertRecipe_AllFlags_ProducesAllConditions()
    {
        // All flags: 0x7F = Good + Centered + Sturdy + Pliant + Malleable + Primed + GoodOmen
        int conditionsFlag = 0x7F;
        var resolver = new ConditionResolver(seed: 42);
        var seen = new HashSet<CraftCondition>();

        for (int i = 0; i < 5000; i++)
        {
            var result = resolver.GetNextCondition(CraftCondition.Normal, isExpert: true, conditionsFlag);
            seen.Add(result);
        }

        seen.Should().Contain(CraftCondition.Normal);
        seen.Should().Contain(CraftCondition.Good);
        seen.Should().Contain(CraftCondition.Centered);
        seen.Should().Contain(CraftCondition.Sturdy);
        seen.Should().Contain(CraftCondition.Pliant);
        seen.Should().Contain(CraftCondition.Malleable);
        seen.Should().Contain(CraftCondition.Primed);
        seen.Should().Contain(CraftCondition.GoodOmen);
    }

    [Fact]
    public void ExpertRecipe_EqualWeightDistribution()
    {
        // Enable Good (0x01) + Centered (0x02) = 3 conditions (Normal + Good + Centered)
        int conditionsFlag = 0x03;
        var resolver = new ConditionResolver(seed: 12345);
        var counts = new Dictionary<CraftCondition, int>
        {
            [CraftCondition.Normal] = 0,
            [CraftCondition.Good] = 0,
            [CraftCondition.Centered] = 0,
        };

        const int iterations = 9000;
        for (int i = 0; i < iterations; i++)
        {
            var result = resolver.GetNextCondition(CraftCondition.Normal, isExpert: true, conditionsFlag);
            counts[result]++;
        }

        // Expected: ~33.3% each (with tolerance)
        double normalPct = (double)counts[CraftCondition.Normal] / iterations * 100;
        double goodPct = (double)counts[CraftCondition.Good] / iterations * 100;
        double centeredPct = (double)counts[CraftCondition.Centered] / iterations * 100;

        normalPct.Should().BeApproximately(33.3, 3.0);
        goodPct.Should().BeApproximately(33.3, 3.0);
        centeredPct.Should().BeApproximately(33.3, 3.0);
    }
}
