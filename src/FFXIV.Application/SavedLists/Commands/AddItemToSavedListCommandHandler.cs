using System.Text.Json;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class AddItemToSavedListCommandHandler
    : IRequestHandler<AddItemToSavedListCommand, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AddItemToSavedListCommandHandler(ISavedListRepository savedListRepo)
    {
        _savedListRepo = savedListRepo;
    }

    public async Task<SavedListDto?> Handle(
        AddItemToSavedListCommand request, CancellationToken cancellationToken)
    {
        var list = await _savedListRepo.GetByIdAsync(request.ListId, request.UserId, cancellationToken);
        if (list is null)
            return null;

        // Parse existing data
        using var doc = JsonDocument.Parse(list.Data);
        var root = doc.RootElement;

        // Build a mutable structure from the JSON
        var dataDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(list.Data, JsonOptions)
            ?? new Dictionary<string, JsonElement>();

        // Find or create materials array
        var materials = new List<Dictionary<string, object>>();
        if (dataDict.TryGetValue("materials", out var materialsElement) &&
            materialsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in materialsElement.EnumerateArray())
            {
                var mat = new Dictionary<string, object>();
                foreach (var prop in item.EnumerateObject())
                {
                    mat[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Number => prop.Value.GetInt32(),
                        JsonValueKind.String => prop.Value.GetString()!,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.GetRawText()
                    };
                }
                materials.Add(mat);
            }
        }

        // Check if item already exists — increment quantity if so
        var existingItem = materials.FirstOrDefault(m =>
            m.TryGetValue("itemId", out var id) && id is int intId && intId == request.ItemId);

        if (existingItem is not null)
        {
            var currentQty = existingItem.TryGetValue("quantity", out var q) && q is int qty ? qty : 0;
            existingItem["quantity"] = currentQty + request.Quantity;
        }
        else
        {
            var newItem = new Dictionary<string, object>
            {
                ["itemId"] = request.ItemId,
                ["itemName"] = request.ItemName,
                ["quantity"] = request.Quantity,
                ["category"] = request.Category ?? "Other"
            };
            materials.Add(newItem);
        }

        // Rebuild JSON with updated materials
        dataDict["materials"] = JsonSerializer.SerializeToElement(materials, JsonOptions);
        list.Data = JsonSerializer.Serialize(dataDict, JsonOptions);
        list.UpdatedAt = DateTimeOffset.UtcNow;

        await _savedListRepo.UpdateAsync(list, cancellationToken);
        return SavedListMapper.ToDto(list);
    }
}
