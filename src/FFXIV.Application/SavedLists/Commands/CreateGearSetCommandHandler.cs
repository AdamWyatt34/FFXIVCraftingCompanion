using System.Text.Json;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class CreateGearSetCommandHandler
    : IRequestHandler<CreateGearSetCommand, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;
    private readonly IEquipmentRepository _equipmentRepo;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CreateGearSetCommandHandler(
        ISavedListRepository savedListRepo,
        IEquipmentRepository equipmentRepo)
    {
        _savedListRepo = savedListRepo;
        _equipmentRepo = equipmentRepo;
    }

    public async Task<SavedListDto?> Handle(
        CreateGearSetCommand request, CancellationToken cancellationToken)
    {
        var count = await _savedListRepo.GetCountByUserAsync(request.UserId, cancellationToken);
        if (count >= 50)
            return null;

        var slots = new Dictionary<string, object>();
        int craftsmanship = 0, control = 0, cp = 0;
        int gathering = 0, perception = 0, gp = 0;
        int maxLevel = 0;

        var equipmentById = (await _equipmentRepo.GetByIdsAsync(request.Slots.Values, cancellationToken))
            .ToDictionary(e => e.EquipmentId);

        foreach (var (slotKey, equipmentId) in request.Slots)
        {
            if (!equipmentById.TryGetValue(equipmentId, out var equipment)) continue;

            slots[slotKey] = new
            {
                EquipmentId = equipmentId,
                equipment.Name,
                equipment.ItemLevel,
                equipment.IconPath
            };

            craftsmanship += equipment.Craftsmanship;
            control += equipment.Control;
            cp += equipment.CP;
            gathering += equipment.Gathering;
            perception += equipment.Perception;
            gp += equipment.GP;

            if (equipment.EquipLevel > maxLevel)
                maxLevel = equipment.EquipLevel;
        }

        var data = new
        {
            Job = request.Job,
            Level = maxLevel,
            Label = request.Name,
            Slots = slots,
            TotalStats = new
            {
                Craftsmanship = craftsmanship,
                Control = control,
                CP = cp,
                Gathering = gathering,
                Perception = perception,
                GP = gp
            }
        };

        var now = DateTimeOffset.UtcNow;
        var savedList = new SavedList
        {
            UserId = request.UserId,
            Name = request.Name,
            Type = SavedListType.GearSet,
            Data = JsonSerializer.Serialize(data, JsonOptions),
            IsPinned = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _savedListRepo.CreateAsync(savedList, cancellationToken);
        return SavedListMapper.ToDto(savedList);
    }
}
