using FFXIV.Application.Gear.DTOs;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed record SearchEquipmentQuery(
    string Query,
    string? Job = null,
    string? Slot = null,
    int? MinLevel = null,
    int? MaxLevel = null) : IRequest<List<EquipmentDto>>;
