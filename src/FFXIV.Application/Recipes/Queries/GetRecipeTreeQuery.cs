using FFXIV.Domain.Models;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed record GetRecipeTreeQuery(int ItemId, int Quantity = 1) : IRequest<MaterialTreeResult>;
