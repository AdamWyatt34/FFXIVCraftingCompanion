namespace FFXIV.Domain.CraftingEngine;

public static class FormulaCalculator
{
    public static int CalculateBaseProgress(
        int craftsmanship, int progressDivider, int progressModifier,
        int crafterLevel, int recipeLevel)
    {
        var baseValue = craftsmanship * 10 / progressDivider + 2;
        if (crafterLevel <= recipeLevel)
            baseValue = baseValue * progressModifier / 100;
        return baseValue;
    }

    public static int CalculateBaseQuality(
        int control, int qualityDivider, int qualityModifier,
        int crafterLevel, int recipeLevel)
    {
        var baseValue = control * 10 / qualityDivider + 35;
        if (crafterLevel <= recipeLevel)
            baseValue = baseValue * qualityModifier / 100;
        return baseValue;
    }
}
