using System.Text;
using System.Text.Json;
using FFXIV.Domain.Models;
using System.Globalization;

namespace FFXIV.Application.CraftPlans;

// Line breaks are '\n' on every platform (never Environment.NewLine), so the output is the same
// bytes the frontend port and the goldens produce.
public static class ShoppingListExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NewLine = "\n",
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ExportAsText(CraftPlanResult result)
    {
        var sb = new StringBuilder();

        // Items to Craft
        sb.Append("=== Items to Craft ===").Append('\n');
        foreach (var item in result.Items)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  {item.ItemName} x{item.Quantity} ({item.CraftType})").Append('\n');
        }
        sb.Append('\n');

        // Intermediate Crafts
        if (result.IntermediateCrafts.Count > 0)
        {
            sb.Append("=== Intermediate Crafts ===").Append('\n');
            foreach (var craft in result.IntermediateCrafts)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  {craft.CraftOrder}. {craft.ItemName} x{craft.TotalQuantity} ({craft.CraftType}, Lv.{craft.RecipeLevel})").Append('\n');
            }
            sb.Append('\n');
        }

        // Raw Materials
        sb.Append("=== Raw Materials ===").Append('\n');
        foreach (var mat in result.RawMaterials)
        {
            var sourceDesc = mat.Sources.Count > 0 ? mat.Sources[0].Description : "";
            sb.Append($"  {mat.ItemName} x{mat.TotalQuantity}" +
                (string.IsNullOrEmpty(sourceDesc) ? "" : $" — {sourceDesc}")).Append('\n');
        }
        sb.Append('\n');

        // Crystals
        if (result.Crystals.Count > 0)
        {
            sb.Append("=== Crystals ===").Append('\n');
            foreach (var crystal in result.Crystals)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  {crystal.ItemName} x{crystal.TotalQuantity}").Append('\n');
            }
            sb.Append('\n');
        }

        // Summary
        sb.Append("=== Summary ===").Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"  Plan Items: {result.Summary.TotalPlanItems}").Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"  Raw Materials: {result.Summary.TotalUniqueRawMaterials} types").Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"  Crystals: {result.Summary.TotalUniqueCrystals} types").Append('\n');
        sb.Append(CultureInfo.InvariantCulture, $"  Intermediate Crafts: {result.Summary.TotalIntermediateCrafts}").Append('\n');

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
            sb.Append(CultureInfo.InvariantCulture, $"{item.ItemId},{item.Quantity}").Append('\n');
        }

        // Intermediate crafts
        foreach (var craft in result.IntermediateCrafts)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{craft.ItemId},{craft.TotalQuantity}").Append('\n');
        }

        // Raw materials
        foreach (var mat in result.RawMaterials)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{mat.ItemId},{mat.TotalQuantity}").Append('\n');
        }

        // Crystals
        foreach (var crystal in result.Crystals)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{crystal.ItemId},{crystal.TotalQuantity}").Append('\n');
        }

        return sb.ToString();
    }
}
