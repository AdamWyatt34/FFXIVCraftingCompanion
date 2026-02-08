using FFXIV.Domain.Models;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed class GetRecipeTreeQueryHandler : IRequestHandler<GetRecipeTreeQuery, MaterialTreeResult>
{
    private readonly IRecipeTreeResolver _resolver;

    public GetRecipeTreeQueryHandler(IRecipeTreeResolver resolver)
    {
        _resolver = resolver;
    }

    public Task<MaterialTreeResult> Handle(GetRecipeTreeQuery request, CancellationToken cancellationToken)
    {
        return _resolver.ResolveTreeAsync(request.ItemId, request.Quantity, cancellationToken);
    }
}
