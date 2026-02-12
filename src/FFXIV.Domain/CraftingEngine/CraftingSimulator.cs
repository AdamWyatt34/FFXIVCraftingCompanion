using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine;

public interface ICraftingSimulator
{
    SimulationResult Simulate(RecipeParameters recipe, CrafterStats stats,
                              IReadOnlyList<int> actionIds, SimulationOptions? options = null);

    StepResult ExecuteAction(CraftingState state, int actionId,
                             CrafterStats stats, RecipeParameters recipe);

    IReadOnlyList<ActionAvailability> GetAvailableActions(CraftingState state,
                                                          CrafterStats stats, RecipeParameters recipe);

    CraftingState CreateInitialState(RecipeParameters recipe, CrafterStats stats);
}

public record SimulationOptions
{
    public bool AssumeAllSuccess { get; init; } = true;
    public bool AssumeNormalCondition { get; init; } = true;
    public int? RandomSeed { get; init; }
    public CraftCondition? ForceCondition { get; init; }
}

public record ActionAvailability
{
    public int ActionId { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public int CPCost { get; init; }
    public int DurabilityCost { get; init; }
    public int SuccessRate { get; init; }
    public ActionCategory Category { get; init; }
}

public class CraftingSimulator : ICraftingSimulator
{
    public CraftingState CreateInitialState(RecipeParameters recipe, CrafterStats stats)
    {
        return CraftingState.CreateInitial(recipe, stats);
    }

    public SimulationResult Simulate(RecipeParameters recipe, CrafterStats stats,
                                      IReadOnlyList<int> actionIds, SimulationOptions? options = null)
    {
        options ??= new SimulationOptions();
        var state = CreateInitialState(recipe, stats);
        var steps = new List<StepResult>();
        var conditionResolver = new ConditionResolver(options.RandomSeed);
        var random = options.RandomSeed.HasValue ? new Random(options.RandomSeed.Value + 1) : new Random();

        foreach (var actionId in actionIds)
        {
            if (state.Status != CraftStatus.InProgress) break;

            var action = CraftingActionRegistry.GetById(actionId);
            if (action == null)
            {
                steps.Add(new StepResult
                {
                    StepNumber = state.Step + 1,
                    ActionName = $"Unknown Action ({actionId})",
                    ActionId = actionId,
                    StateBefore = state,
                    StateAfter = state,
                    ActionSucceeded = false,
                    FailReason = "Unknown action"
                });
                continue;
            }

            var stepResult = ExecuteActionInternal(state, action, stats, recipe, options, conditionResolver, random);
            steps.Add(stepResult);
            state = stepResult.StateAfter;
        }

        return new SimulationResult
        {
            Steps = steps,
            FinalState = state
        };
    }

    public StepResult ExecuteAction(CraftingState state, int actionId,
                                     CrafterStats stats, RecipeParameters recipe)
    {
        var action = CraftingActionRegistry.GetById(actionId);
        if (action == null)
        {
            return new StepResult
            {
                StepNumber = state.Step + 1,
                ActionName = $"Unknown Action ({actionId})",
                ActionId = actionId,
                StateBefore = state,
                StateAfter = state,
                ActionSucceeded = false,
                FailReason = "Unknown action"
            };
        }

        var failReason = action.GetFailReason(state, stats, recipe);
        if (failReason != null)
        {
            return new StepResult
            {
                StepNumber = state.Step + 1,
                ActionName = action.InGameName,
                ActionId = action.Id,
                StateBefore = state,
                StateAfter = state,
                ActionSucceeded = false,
                FailReason = failReason
            };
        }

        var stateBefore = state;
        var stateAfter = action.Execute(state, stats, recipe, forceSuccess: true);

        return new StepResult
        {
            StepNumber = stateBefore.Step + 1,
            ActionName = action.InGameName,
            ActionId = action.Id,
            StateBefore = stateBefore,
            StateAfter = stateAfter,
            ActionSucceeded = true
        };
    }

    public IReadOnlyList<ActionAvailability> GetAvailableActions(CraftingState state,
                                                                  CrafterStats stats, RecipeParameters recipe)
    {
        var allActions = CraftingActionRegistry.GetAll();
        var result = new List<ActionAvailability>(allActions.Count);

        foreach (var action in allActions)
        {
            var failReason = action.GetFailReason(state, stats, recipe);
            result.Add(new ActionAvailability
            {
                ActionId = action.Id,
                ActionName = action.InGameName,
                IsAvailable = failReason == null,
                UnavailableReason = failReason,
                CPCost = action.GetCPCost(state),
                DurabilityCost = action.GetDurabilityCost(state),
                SuccessRate = action.GetSuccessRate(state),
                Category = action.Category
            });
        }

        return result;
    }

    private StepResult ExecuteActionInternal(CraftingState state, ICraftingAction action,
                                              CrafterStats stats, RecipeParameters recipe,
                                              SimulationOptions options, ConditionResolver conditionResolver,
                                              Random random)
    {
        // a. Check CanExecute
        var failReason = action.GetFailReason(state, stats, recipe);
        if (failReason != null)
        {
            return new StepResult
            {
                StepNumber = state.Step + 1,
                ActionName = action.InGameName,
                ActionId = action.Id,
                StateBefore = state,
                StateAfter = state,
                ActionSucceeded = false,
                FailReason = failReason
            };
        }

        // b. Determine success
        bool succeeded;
        if (options.AssumeAllSuccess)
        {
            succeeded = true;
        }
        else
        {
            int successRate = action.GetSuccessRate(state);
            succeeded = random.NextDouble() * 100.0 < successRate;
        }

        // c. Execute with success/failure flag
        var stateBefore = state;
        var stateAfter = action.Execute(state, stats, recipe, forceSuccess: succeeded);

        // d. Update condition for next step
        if (options.ForceCondition.HasValue)
        {
            stateAfter = stateAfter with { Condition = options.ForceCondition.Value };
        }
        else if (!options.AssumeNormalCondition)
        {
            var nextCondition = conditionResolver.GetNextCondition(
                stateAfter.Condition, recipe.IsExpert, recipe.ConditionsFlag);
            stateAfter = stateAfter with { Condition = nextCondition };
        }

        // e. Return StepResult
        return new StepResult
        {
            StepNumber = stateBefore.Step + 1,
            ActionName = action.InGameName,
            ActionId = action.Id,
            StateBefore = stateBefore,
            StateAfter = stateAfter,
            ActionSucceeded = succeeded,
            FailReason = succeeded ? null : "Action failed (success rate roll)"
        };
    }
}
