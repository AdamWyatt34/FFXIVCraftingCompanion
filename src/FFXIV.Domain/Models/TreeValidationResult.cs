namespace FFXIV.Domain.Models;

public class TreeValidationResult
{
    public int RecipeId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<TreeDiscrepancy> Discrepancies { get; set; } = [];
    public int OurTotalRawMaterials { get; set; }
    public int GarlandTotalRawMaterials { get; set; }
    public TimeSpan ValidationDuration { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TreeDiscrepancy
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int OurQuantity { get; set; }
    public int GarlandQuantity { get; set; }
    public string DiscrepancyType { get; set; } = string.Empty;
}

public class BatchValidationResult
{
    public int TotalValidated { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<TreeValidationResult> Results { get; set; } = [];
    public TimeSpan TotalDuration { get; set; }
    public double PassRate => TotalValidated > 0
        ? (double)PassedCount / TotalValidated * 100
        : 0;
}
