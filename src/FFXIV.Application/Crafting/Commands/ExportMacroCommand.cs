using FFXIV.Application.Crafting.DTOs;
using FFXIV.Domain.CraftingEngine;
using MediatR;

namespace FFXIV.Application.Crafting.Commands;

public sealed record ExportMacroCommand(ExportMacroRequest Request)
    : IRequest<(ExportMacroResultDto? Result, string? Error)>;

public sealed class ExportMacroCommandHandler
    : IRequestHandler<ExportMacroCommand, (ExportMacroResultDto? Result, string? Error)>
{
    public Task<(ExportMacroResultDto? Result, string? Error)> Handle(
        ExportMacroCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        if (req.ActionIds.Count == 0)
            return Task.FromResult<(ExportMacroResultDto?, string?)>(
                (null, "At least one action ID is required."));

        if (req.ActionIds.Count > 100)
            return Task.FromResult<(ExportMacroResultDto?, string?)>(
                (null, "Action sequence exceeds maximum of 100 actions."));

        foreach (var actionId in req.ActionIds)
        {
            if (CraftingActionRegistry.GetById(actionId) is null)
                return Task.FromResult<(ExportMacroResultDto?, string?)>(
                    (null, $"Action ID {actionId} not found."));
        }

        var options = CraftingMapper.ToDomain(req.Options);
        var macros = MacroExporter.ExportToMacro(req.ActionIds, options);

        var result = new ExportMacroResultDto
        {
            Macros = macros,
            TotalActions = req.ActionIds.Count,
            MacroCount = macros.Count,
        };

        return Task.FromResult<(ExportMacroResultDto?, string?)>((result, null));
    }
}
