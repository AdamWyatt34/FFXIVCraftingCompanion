using System.Text;
using System.Text.Json;
using FFXIV.Domain.Models;

namespace FFXIV.Application.CraftPlans;

public static class ShoppingListExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string ExportAsText(CraftPlanResult result)
    {
        var sb = new StringBuilder();

        // Items to Craft
        sb.AppendLine("=== Items to Craft ===");
        foreach (var item in result.Items)
        {
            sb.AppendLine($"  {item.ItemName} x{item.Quantity} ({item.CraftType})");
        }
        sb.AppendLine();

        // Intermediate Crafts
        if (result.IntermediateCrafts.Count > 0)
        {
            sb.AppendLine("=== Intermediate Crafts ===");
            foreach (var craft in result.IntermediateCrafts)
            {
                sb.AppendLine($"  {craft.CraftOrder}. {craft.ItemName} x{craft.TotalQuantity} ({craft.CraftType}, Lv.{craft.RecipeLevel})");
            }
            sb.AppendLine();
        }

        // Raw Materials
        sb.AppendLine("=== Raw Materials ===");
        foreach (var mat in result.RawMaterials)
        {
            var sourceDesc = mat.Sources.Count > 0 ? mat.Sources[0].Description : "";
            sb.AppendLine($"  {mat.ItemName} x{mat.TotalQuantity}" +
                (string.IsNullOrEmpty(sourceDesc) ? "" : $" — {sourceDesc}"));
        }
        sb.AppendLine();

        // Crystals
        if (result.Crystals.Count > 0)
        {
            sb.AppendLine("=== Crystals ===");
            foreach (var crystal in result.Crystals)
            {
                sb.AppendLine($"  {crystal.ItemName} x{crystal.TotalQuantity}");
            }
            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("=== Summary ===");
        sb.AppendLine($"  Plan Items: {result.Summary.TotalPlanItems}");
        sb.AppendLine($"  Raw Materials: {result.Summary.TotalUniqueRawMaterials} types");
        sb.AppendLine($"  Crystals: {result.Summary.TotalUniqueCrystals} types");
        sb.AppendLine($"  Intermediate Crafts: {result.Summary.TotalIntermediateCrafts}");

        return sb.ToString();
    }

    public static string ExportAsJson(CraftPlanResult result)
    {
        var dto = CraftPlanMapper.ToResultDto(result);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static string ExportAsTeamcraft(CraftPlanResult result)
    {
        var sb = new StringBuilder();

        // Final items
        foreach (var item in result.Items)
        {
            sb.AppendLine($"{item.ItemId},{item.Quantity}");
        }

        // Intermediate crafts
        foreach (var craft in result.IntermediateCrafts)
        {
            sb.AppendLine($"{craft.ItemId},{craft.TotalQuantity}");
        }

        // Raw materials
        foreach (var mat in result.RawMaterials)
        {
            sb.AppendLine($"{mat.ItemId},{mat.TotalQuantity}");
        }

        // Crystals
        foreach (var crystal in result.Crystals)
        {
            sb.AppendLine($"{crystal.ItemId},{crystal.TotalQuantity}");
        }

        return sb.ToString();
    }
}
