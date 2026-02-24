using System.Text.Json;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class RemoveItemFromSavedListCommandHandler
    : IRequestHandler<RemoveItemFromSavedListCommand, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RemoveItemFromSavedListCommandHandler(ISavedListRepository savedListRepo)
    {
        _savedListRepo = savedListRepo;
    }

    public async Task<SavedListDto?> Handle(
        RemoveItemFromSavedListCommand request, CancellationToken cancellationToken)
    {
        var list = await _savedListRepo.GetByIdAsync(request.ListId, request.UserId, cancellationToken);
        if (list is null)
            return null;

        var dataDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(list.Data, JsonOptions)
            ?? new Dictionary<string, JsonElement>();

        if (!dataDict.TryGetValue("materials", out var materialsElement) ||
            materialsElement.ValueKind != JsonValueKind.Array)
        {
            return SavedListMapper.ToDto(list);
        }

        // Filter out the item with matching itemId
        var materials = new List<Dictionary<string, object>>();
        foreach (var item in materialsElement.EnumerateArray())
        {
            var itemId = item.TryGetProperty("itemId", out var idProp) ? idProp.GetInt32() : 0;
            if (itemId == request.ItemId)
                continue;

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

        dataDict["materials"] = JsonSerializer.SerializeToElement(materials, JsonOptions);
        list.Data = JsonSerializer.Serialize(dataDict, JsonOptions);
        list.UpdatedAt = DateTimeOffset.UtcNow;

        await _savedListRepo.UpdateAsync(list, cancellationToken);
        return SavedListMapper.ToDto(list);
    }
}
