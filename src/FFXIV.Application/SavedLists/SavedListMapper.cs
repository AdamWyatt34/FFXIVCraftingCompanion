using System.Text.Json;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Entities;

namespace FFXIV.Application.SavedLists;

public static class SavedListMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static SavedListDto ToDto(SavedList entity)
    {
        object data;
        try
        {
            data = JsonSerializer.Deserialize<JsonElement>(entity.Data, JsonOptions);
        }
        catch
        {
            data = entity.Data;
        }

        return new SavedListDto(
            Id: entity.Id,
            Name: entity.Name,
            Type: entity.Type.ToString(),
            Data: data,
            IsPinned: entity.IsPinned,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt);
    }

    public static SavedListSummaryDto ToSummaryDto(SavedList entity)
    {
        return new SavedListSummaryDto(
            Id: entity.Id,
            Name: entity.Name,
            Type: entity.Type.ToString(),
            IsPinned: entity.IsPinned,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt);
    }
}
