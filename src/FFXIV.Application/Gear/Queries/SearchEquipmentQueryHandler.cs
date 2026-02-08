using System.Security.Cryptography;
using System.Text;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed class SearchEquipmentQueryHandler
    : IRequestHandler<SearchEquipmentQuery, List<EquipmentDto>>
{
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly ICacheService _cache;

    public SearchEquipmentQueryHandler(
        IEquipmentRepository equipmentRepo,
        ICacheService cache)
    {
        _equipmentRepo = equipmentRepo;
        _cache = cache;
    }

    public async Task<List<EquipmentDto>> Handle(
        SearchEquipmentQuery request, CancellationToken cancellationToken)
    {
        var queryPart = request.Query.ToLowerInvariant();
        var raw = $"{queryPart}:{request.Job}:{request.Slot}:{request.MinLevel}:{request.MaxLevel}";
        var queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var cacheKey = $"gearsearch:{queryHash}";

        var cached = await _cache.GetAsync<List<EquipmentDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        EquipSlot? slot = null;
        if (!string.IsNullOrEmpty(request.Slot) &&
            Enum.TryParse<EquipSlot>(request.Slot, ignoreCase: true, out var parsedSlot))
        {
            slot = parsedSlot;
        }

        var results = await _equipmentRepo.SearchEquipmentAsync(
            request.Query,
            request.Job,
            slot,
            request.MinLevel,
            request.MaxLevel,
            cancellationToken: cancellationToken);

        var dtos = results.Select(GearMapper.ToDto).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromHours(1), cancellationToken);
        return dtos;
    }
}
