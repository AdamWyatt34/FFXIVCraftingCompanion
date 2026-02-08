using FFXIV.Application.Recipes.DTOs;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed record GetMaterialTreeQuery(int RecipeId, int Quantity = 1)
    : IRequest<MaterialTreeResultDto?>;
