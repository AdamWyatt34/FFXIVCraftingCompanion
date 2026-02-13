using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Infrastructure.Solver.MCTS;

internal sealed class MCTSNode
{
    public MCTSNode? Parent { get; set; }
    public int ActionId { get; set; }
    public required CraftingState State { get; set; }
    public List<MCTSNode> Children { get; set; } = [];

    // MCTS statistics
    public int VisitCount { get; set; }
    public double TotalReward { get; set; }
    public double AverageReward => VisitCount > 0 ? TotalReward / VisitCount : 0;
    public double BestReward { get; set; } = double.MinValue;

    // Unexplored actions from this state
    public List<int> UntriedActions { get; set; } = [];

    // Terminal: craft complete or failed
    public bool IsTerminal => State.Status != CraftStatus.InProgress;
    public bool IsFullyExpanded => UntriedActions.Count == 0;

    /// <summary>
    /// UCB1 selection score (Upper Confidence Bound).
    /// </summary>
    public double UCB1Score(double explorationConstant)
    {
        if (VisitCount == 0) return double.MaxValue;
        if (Parent == null) return AverageReward;

        return AverageReward +
               explorationConstant * Math.Sqrt(
                   Math.Log(Parent.VisitCount) / VisitCount
               );
    }
}
