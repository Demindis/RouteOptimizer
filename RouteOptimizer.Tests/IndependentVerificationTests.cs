using RouteOptimizer.Application;
using RouteOptimizer.Domain;

namespace RouteOptimizer.Tests;

public sealed class IndependentVerificationTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void ExactSearch_MatchesIndependentSubsetDynamicProgramming(int size)
    {
        for (uint seed = 1; seed <= 12; seed++)
        {
            var sample = ProblemSamples.Experiment(size, seed);
            var matrix = sample.CopyCosts();
            // Some cases are sparse, with zero costs and a non-default start.
            if (seed % 2 == 0)
                for (var i = 0; i < size; i++)
                for (var j = 0; j < size; j++)
                    if (i != j && (i * 7 + j * 11 + seed) % 4 == 0) matrix[i, j] = null;
            if (seed % 3 == 0) matrix[0, 1] = 0;
            var problem = new TspProblem(sample.Names, matrix, (int)seed % size);
            var expected = HeldKarpCost(problem);
            var session = new LittleSolver().CreateSession(problem);
            while (session.Result.Status == SearchStatus.Running)
            {
                if (expected.HasValue)
                {
                    Assert.True(session.Result.LowerBound <= expected, $"Lower bound exceeded optimum: n={size}, seed={seed}");
                    if (session.Result.BestRoute is { } route) Assert.True(route.Cost >= expected);
                }
                session.Advance();
            }
            Assert.Equal(expected.HasValue ? SearchStatus.Optimal : SearchStatus.Infeasible, session.Result.Status);
            Assert.Equal(expected, session.Result.BestRoute?.Cost);
            if (expected.HasValue) Assert.Equal(expected, session.Result.LowerBound);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    public void ExperimentSamples_AreReproducibleAndFinishExactly(int size)
    {
        var first = ProblemSamples.Experiment(size);
        var second = ProblemSamples.Experiment(size);
        Assert.Equal(first.CopyCosts().Cast<int?>(), second.CopyCosts().Cast<int?>());
        var session = new LittleSolver().CreateSession(first, new SearchOptions(MaxDetailedSteps: 0));
        SolverTests.Finish(session);
        Assert.Equal(SearchStatus.Optimal, session.Result.Status);
        Assert.Equal(HeldKarpCost(first), session.Result.BestRoute!.Cost);
        Assert.Empty(session.Steps);
    }

    // Independent test oracle; it shares no reduction, branching or subtour code with Little.
    private static long? HeldKarpCost(TspProblem problem)
    {
        var memo = new Dictionary<(int Vertex, int Mask), long?>();
        long? Visit(int vertex, int mask)
        {
            if (mask == (1 << problem.Count) - 1) return problem[vertex, problem.Start];
            if (memo.TryGetValue((vertex, mask), out var cached)) return cached;
            long? best = null;
            for (var next = 0; next < problem.Count; next++)
            {
                if ((mask & (1 << next)) != 0 || problem[vertex, next] is not { } edge) continue;
                if (Visit(next, mask | (1 << next)) is { } tail && (best is null || edge + tail < best)) best = edge + tail;
            }
            return memo[(vertex, mask)] = best;
        }
        return Visit(problem.Start, 1 << problem.Start);
    }
}
