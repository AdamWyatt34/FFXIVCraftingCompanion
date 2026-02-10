using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Queries;

public sealed record PreviewLodestoneQuery(
    string CharacterName,
    string ServerName) : IRequest<LodestonePreviewDto?>;
