using FFXIV.Application.DTOs;
using FFXIV.Application.Journey.DTOs;
using FFXIV.Application.Mapping;
using FFXIV.Application.Recipes;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Journey.Queries;

public sealed record GetCraftingJourneyQuery(int RecipeId, string? UserId, bool IsSpecialist = false)
    : IRequest<(CraftingJourneyDto? Result, string? Error)>;

public sealed class GetCraftingJourneyQueryHandler
    : IRequestHandler<GetCraftingJourneyQuery, (CraftingJourneyDto? Result, string? Error)>
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly IRecipeTreeResolver _treeResolver;
    private readonly IContextualLinkService _linkService;
    private readonly IStatSufficiencyService _sufficiencyService;
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly ILogger<GetCraftingJourneyQueryHandler> _logger;

    public GetCraftingJourneyQueryHandler(
        IRecipeRepository recipeRepo,
        IEquipmentRepository equipmentRepo,
        IRecipeTreeResolver treeResolver,
        IContextualLinkService linkService,
        IStatSufficiencyService sufficiencyService,
        ICharacterProfileRepository profileRepo,
        ILogger<GetCraftingJourneyQueryHandler> logger)
    {
        _recipeRepo = recipeRepo;
        _equipmentRepo = equipmentRepo;
        _treeResolver = treeResolver;
        _linkService = linkService;
        _sufficiencyService = sufficiencyService;
        _profileRepo = profileRepo;
        _logger = logger;
    }

    public async Task<(CraftingJourneyDto? Result, string? Error)> Handle(
        GetCraftingJourneyQuery request, CancellationToken cancellationToken)
    {
        var recipe = await _recipeRepo.GetByIdAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return (null, $"Recipe {request.RecipeId} not found.");

        // Build recipe DTO
        var ingredients = recipe.Ingredients
            .Select(ri => Mapper.ToDto(ri, ri.Item))
            .ToList();
        var recipeDto = Mapper.ToDto(recipe, recipe.ResultItem, ingredients);

        // Check equipment info
        var equipment = await _equipmentRepo.GetByRecipeIdAsync(request.RecipeId, cancellationToken);
        if (equipment is not null)
        {
            recipeDto = recipeDto with
            {
                EquipmentInfo = new RecipeEquipmentInfoDto(
                    equipment.EquipmentId,
                    equipment.EquipSlot.ToString(),
                    equipment.ClassJobCategory,
                    equipment.Craftsmanship,
                    equipment.Control,
                    equipment.CP,
                    equipment.Gathering,
                    equipment.Perception,
                    equipment.GP)
            };
        }

        // Run sequentially to avoid DbContext threading issues
        var materials = await _treeResolver.ResolveTreeAsync(recipe.ResultItemId, 1, cancellationToken);
        var context = await _linkService.GetLinksForItemAsync(recipe.ResultItemId, cancellationToken);

        // Optionally resolve sufficiency if user is authenticated
        StatSufficiencyResultDto? sufficiency = null;
        if (!string.IsNullOrEmpty(request.UserId))
        {
            sufficiency = await ResolveSufficiencyAsync(request.RecipeId, request.UserId, request.IsSpecialist, cancellationToken);
        }

        var materialsDto = MaterialTreeMapper.ToDto(materials, 1);
        var contextDto = JourneyMapper.ToDto(context);

        var journeyDto = new CraftingJourneyDto(
            recipeDto,
            materialsDto,
            sufficiency,
            sufficiency?.SuggestedGearUpgrades ?? [],
            contextDto);

        return (journeyDto, null);
    }

    private async Task<StatSufficiencyResultDto?> ResolveSufficiencyAsync(
        int recipeId, string userId, bool isSpecialist, CancellationToken ct)
    {
        try
        {
            var profile = await _profileRepo.GetByUserIdAsync(userId, ct);
            if (profile is null)
                return null;

            var stats = new CrafterStats
            {
                Level = profile.ActiveJobLevel,
                Craftsmanship = profile.Craftsmanship,
                Control = profile.Control,
                CP = profile.CP,
                IsSpecialist = isSpecialist,
            };

            var result = await _sufficiencyService.AnalyzeAsync(recipeId, stats, profile, ct);
            return JourneyMapper.ToDto(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sufficiency analysis failed for recipe {RecipeId}, user {UserId}", recipeId, userId);
            return null;
        }
    }
}
