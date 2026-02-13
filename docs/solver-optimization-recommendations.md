# Solver Optimization Recommendations

Generated from benchmark suite results (2026-02-13).

## Baseline Benchmark Results

### Quality Comparison: Community vs Greedy vs MCTS (10s budget)

| Benchmark                   | Community Q% | Greedy Q% | MCTS Q%  | MCTS/Community |
|-----------------------------|-------------|-----------|----------|----------------|
| lv100_4star_endgame         |       33.6% |     31.7% |    62.2% |         185.2% |
| lv90_3star_standard         |       30.6% |     43.9% |    62.3% |         203.7% |
| lv80_2star_overgeared       |       99.3% |    100.0% |   100.0% |         100.7% |
| lv70_standard               |       60.1% |     37.8% |    95.5% |         159.0% |
| lv60_standard               |       54.2% |     70.5% |    92.8% |         171.4% |
| lv50_classic                |       93.4% |     59.5% |   100.0% |         107.0% |
| lv90_35dur_food             |       48.5% |     44.0% |    92.9% |         191.6% |
| lv100_40dur_potion          |       30.0% |     15.7% |    66.4% |         221.7% |
| lv100_undergeared           |       28.9% |     41.5% |    58.9% |         203.6% |
| lv100_4star_progress_heavy  |       21.2% |     30.4% |    48.1% |         227.4% |

### Key Findings

- **MCTS consistently outperforms both community rotations and greedy** across all 10 benchmarks
- MCTS vs community quality ratio: **100.7% - 227.4%** (average ~177%)
- **Greedy solver is inconsistent**: beats community in 5/10 cases but falls short in others
  - Greedy excels at overgeared/easy recipes (100% on lv80)
  - Greedy struggles with low-durability recipes (15.7% on lv100_40dur_potion)
- **Low-durability recipes** (35dur, 40dur) show the biggest MCTS advantage, suggesting MCTS handles resource management significantly better than greedy heuristics
- **Undergeared/progress-heavy scenarios** also favor MCTS, likely due to better long-term planning over greedy's short-term scoring

## Performance Profile

### Greedy Solver Performance

| Benchmark                   | Time(ms) | Quality% | Steps |
|-----------------------------|----------|----------|-------|
| lv100_4star_endgame         |      <15 |    31.7% |    17 |
| lv90_3star_standard         |       <1 |    43.9% |    15 |
| lv80_2star_overgeared       |       <1 |   100.0% |     3 |
| All others                  |       <1 |  various |  varies|

**Verdict**: Greedy is effectively instant (<1ms for most, 15ms worst case). No optimization needed for the greedy solver's speed.

### MCTS Performance

| Benchmark                   | Time(ms) | Iterations | Iter/sec | Quality% | Steps |
|-----------------------------|----------|------------|----------|----------|-------|
| lv100_4star_endgame         |    10001 |     119,889|   11,988 |    62.2% |    26 |
| lv90_3star_standard         |    10000 |     145,267|   14,527 |    62.3% |    21 |
| lv80_2star_overgeared       |    10000 |     461,046|   46,105 |   100.0% |     3 |
| lv70_standard               |    10000 |     110,261|   11,026 |    95.5% |    19 |
| lv60_standard               |    10000 |     159,397|   15,940 |    92.8% |    17 |
| lv50_classic                |    10000 |     198,614|   19,861 |   100.0% |    14 |
| lv90_35dur_food             |    10000 |     232,775|   23,278 |    96.0% |    13 |
| lv100_40dur_potion          |    10000 |     220,879|   22,088 |    66.4% |    23 |
| lv100_undergeared           |    10000 |     139,507|   13,951 |    58.9% |    20 |
| lv100_4star_progress_heavy  |    10000 |     131,117|   13,112 |    47.9% |    19 |

**Iteration rates**: 11K - 46K iter/sec depending on recipe complexity.
- Simple recipes (overgeared, low level): 20K-46K iter/sec
- Complex recipes (4-star, undergeared): 11K-14K iter/sec
- The difference is driven by average rollout length (more complex = deeper rollouts)

### Memory Usage

- MCTS 10s run (lv100_4star_endgame): **~42 MB delta**, 90K iterations
- Approximately **487 bytes/iteration**
- Memory is dominated by tree node allocations (state copies + child arrays)
- For a 10s budget this is acceptable; for longer runs consider node pooling or limiting tree depth

## Greedy Solver Observations

### Strengths
- Instant execution (<1ms typical)
- Excellent at overgeared/trivial recipes (100% quality)
- Good at finding finishing sequences that complete progress

### Weaknesses
1. **Durability management is poor**: Greedy doesn't plan ahead for durability, sometimes exhausting it before quality phase
2. **Quality phase heuristics are too conservative**: Byregot's Blessing threshold (IQ >= 8 + GreatStrides) is hard to reach in shorter rotations
3. **Low-durability recipes**: 15.7% quality on lv100_40dur shows greedy can't effectively plan around tight constraints
4. **Mid-level toolkit mismatch**: At lv70, greedy scores 37.8% vs community's 60.1%, suggesting the action scoring doesn't adapt well to limited action pools

### Potential Greedy Tuning Areas
- **`FinishingCPBuffer` (default: 50)**: May be too conservative for high-CP builds, leaving quality CP unspent
- **`DurabilityBuffer` (default: 10)**: Consider increasing for low-durability recipes
- **`ByregotsHighIQThreshold` (default: 8)**: May be too high for shorter rotations; try 6
- **`ManipulationScore` (default: 78)**: Consider boosting for low-durability recipes where Manipulation is critical
- **Action scoring doesn't consider remaining durability**: Adding durability-awareness to quality action scoring could significantly improve low-dur performance

## MCTS Observations

### Current Hyperparameters
| Parameter                     | Value | Notes |
|-------------------------------|-------|-------|
| ExplorationConstant           | 1.41  | Standard UCB1 sqrt(2) |
| RolloutTemperature            | 0.5   | Moderately greedy rollouts |
| HeuristicExpansionProbability | 0.7   | 70% heuristic, 30% random expansion |
| CompletedQualityWeight        | 0.8   | Strongly quality-focused reward |
| StepPenaltyPerStep            | 0.001 | Very light step penalty |

### Potential MCTS Tuning Areas

1. **ExplorationConstant**: The default 1.41 (sqrt(2)) is standard but may benefit from lowering to ~1.0 for faster convergence on exploitation. Run the `TuneExplorationConstant` test to compare.

2. **RolloutTemperature**: At 0.5, rollouts are moderately greedy. Lower values (0.25) make rollouts more deterministic (exploit known-good moves), while higher values (1.0+) explore more diverse strategies. For endgame recipes where the search space is large, slightly higher temperature may help.

3. **Reward function**: The current `CompletedQualityWeight=0.8` vs `CompletedBaseReward=0.2` split means quality dominates the reward. For progress-heavy recipes (lv100_4star_progress_heavy at 47.9%), consider increasing the base reward for completion or adding a progress-proportional component to the in-progress reward.

4. **Step penalty**: At 0.001 per step, the penalty is minimal. This allows the solver to use longer rotations, which is generally good. No change recommended.

5. **Iteration budget**: The 10s budget achieves 90K-460K iterations depending on complexity. For the hardest recipes (lv100_4star), even 120K iterations produces solid results (62.2%). A 5s budget would be acceptable for most use cases; 10s recommended for 4-star recipes.

## Recommendations

### Immediate (No tuning required)
1. **Use MCTS for all solver requests** - It consistently outperforms greedy and community rotations
2. **Default time budget: 5s for standard recipes, 10s for starred/expert recipes** - Based on iteration rate vs quality tradeoff
3. **Greedy as instant fallback** - Use for real-time preview/suggestions during manual rotation building

### Short-term (Parameter tuning)
1. Run `MCTSParameterTuningTests` and compare results across 3 representative benchmarks
2. Focus on `ExplorationConstant` and `RolloutTemperature` first - these have the most impact
3. Consider lowering `ByregotsHighIQThreshold` from 8 to 6 in GreedySolverConfig for better greedy baseline

### Medium-term (Architecture improvements)
1. **Adaptive time budgets**: Use recipe difficulty/stars to auto-select 5s vs 10s
2. **Greedy durability awareness**: Add remaining-durability factor to action scoring
3. **Node pooling for MCTS**: Reduce GC pressure at ~487 bytes/iteration
4. **Early termination**: Stop MCTS when quality reaches 100% or hasn't improved for N iterations

### Long-term (Advanced)
1. **Neural network policy**: Replace heuristic expansion with a trained policy network
2. **Parallel MCTS**: Root parallelization across CPU cores
3. **Opening book**: Cache optimal opening sequences for common recipe parameters
4. **Progressive widening**: Limit branching factor in early tree levels for deeper search

## How to Run Benchmarks

```bash
# Core benchmark tests (community + greedy + MCTS, ~3 minutes)
dotnet test tests/FFXIV.Tests/ --filter "FullyQualifiedName~SolverBenchmarks" -v n

# Quality comparison report
dotnet test tests/FFXIV.Tests/ --filter "Category=Report" --logger "console;verbosity=detailed"

# Parameter tuning sweeps (~30 minutes)
dotnet test tests/FFXIV.Tests/ --filter "Category=Tuning" --logger "console;verbosity=detailed"

# Performance profiling (~2 minutes)
dotnet test tests/FFXIV.Tests/ --filter "Category=Performance" --logger "console;verbosity=detailed"
```
