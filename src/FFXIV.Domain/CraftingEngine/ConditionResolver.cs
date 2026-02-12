using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine;

public class ConditionResolver
{
    private readonly Random _random;

    public ConditionResolver(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public CraftCondition GetNextCondition(CraftCondition current, bool isExpert, int conditionsFlag = 0)
    {
        // Deterministic transitions
        if (current == CraftCondition.Excellent) return CraftCondition.Poor;
        if (current == CraftCondition.Good) return CraftCondition.Normal;
        if (current == CraftCondition.Poor) return CraftCondition.Normal;
        if (current == CraftCondition.GoodOmen) return CraftCondition.Good;

        if (isExpert)
            return GetNextExpertCondition(conditionsFlag);

        return GetNextStandardCondition();
    }

    private CraftCondition GetNextStandardCondition()
    {
        double roll = _random.NextDouble();
        if (roll < 0.04) return CraftCondition.Excellent;
        if (roll < 0.24) return CraftCondition.Good;
        return CraftCondition.Normal;
    }

    private CraftCondition GetNextExpertCondition(int conditionsFlag)
    {
        var conditions = new List<(CraftCondition condition, double weight)>
        {
            (CraftCondition.Normal, 1.0)
        };

        if ((conditionsFlag & 0x01) != 0) conditions.Add((CraftCondition.Good, 1.0));
        if ((conditionsFlag & 0x02) != 0) conditions.Add((CraftCondition.Centered, 1.0));
        if ((conditionsFlag & 0x04) != 0) conditions.Add((CraftCondition.Sturdy, 1.0));
        if ((conditionsFlag & 0x08) != 0) conditions.Add((CraftCondition.Pliant, 1.0));
        if ((conditionsFlag & 0x10) != 0) conditions.Add((CraftCondition.Malleable, 1.0));
        if ((conditionsFlag & 0x20) != 0) conditions.Add((CraftCondition.Primed, 1.0));
        if ((conditionsFlag & 0x40) != 0) conditions.Add((CraftCondition.GoodOmen, 1.0));

        double totalWeight = conditions.Sum(c => c.weight);
        double roll = _random.NextDouble() * totalWeight;
        double cumulative = 0;

        foreach (var (condition, weight) in conditions)
        {
            cumulative += weight;
            if (roll < cumulative) return condition;
        }

        return CraftCondition.Normal;
    }
}
