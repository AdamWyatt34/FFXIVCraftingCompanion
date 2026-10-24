namespace FFXIV.Domain.CraftingEngine;

public static class FormulaCalculator
{
    // The modifier applies at or below the recipe's class job level, never its rlvl (0: no level row),
    // and the value truncates once (Raphael raphael-data/src/lib.rs:145-149,175-192; Teamcraft getBaseProgression).
    public static int CalculateBaseProgress(
        int craftsmanship, int progressDivider, int progressModifier,
        int crafterLevel, int classJobLevel)
    {
        var baseValue = craftsmanship * 10.0 / progressDivider + 2;
        if (classJobLevel > 0 && crafterLevel <= classJobLevel)
            baseValue = baseValue * progressModifier / 100;
        return (int)baseValue;
    }

    public static int CalculateBaseQuality(
        int control, int qualityDivider, int qualityModifier,
        int crafterLevel, int classJobLevel)
    {
        var baseValue = control * 10.0 / qualityDivider + 35;
        if (classJobLevel > 0 && crafterLevel <= classJobLevel)
            baseValue = baseValue * qualityModifier / 100;
        return (int)baseValue;
    }
}
