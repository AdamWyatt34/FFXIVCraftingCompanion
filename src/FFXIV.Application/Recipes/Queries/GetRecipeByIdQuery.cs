using FFXIV.Application.DTOs;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed record GetRecipeByIdQuery(int RecipeId, bool IncludeMaterials = false) : IRequest<RecipeDto?>;
