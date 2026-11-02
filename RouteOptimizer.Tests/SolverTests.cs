using RouteOptimizer.Application;
using RouteOptimizer.Domain;

namespace RouteOptimizer.Tests;

public sealed class SolverTests
{
    [Fact]
    public void Search_StartsWithoutIncumbentAndFindsRoute()
    {
        var session = Create(ProblemSamples.TeachingExample());
        Assert.Null(session.Result.BestRoute);
        Assert.Equal(36, session.Result.LowerBound);
        Assert.Null(session.Result.AbsoluteGap);
        Assert.Null(session.Result.RelativeGapPercent);
        session.Advance();
        Assert.Null(session.Result.BestRoute);
        Finish(session);
        Assert.Equal(SearchStatus.Optimal, session.Result.Status);
        Assert.Equal(39, session.Result.BestRoute!.Cost);
    }

    [Fact]
    public void EqualPenaltiesAndBounds_UseVertexAndCreationOrder()
    {
        int?[,] matrix = { { null, 1, 1, 1 }, { 1, null, 1, 1 }, { 1, 1, null, 1 }, { 10, 1, 1, null } };
        var session = Create(Problem(matrix));
        session.Advance();
        var root = Assert.Single(session.Steps);
        Assert.All(root.Penalties, p => Assert.Equal(0, p.Penalty));
        Assert.Equal(new Arc(0, 1), root.Branch);
        Assert.Equal(root.Children[0].LowerBound, root.Children[1].LowerBound);
        session.Advance();
        Assert.Equal(root.Children[0].NodeId, session.Steps[1].NodeId);
        Finish(session);
        Assert.Equal(4, session.Result.BestRoute!.Cost);
    }

    [Fact]
    public void DisconnectedCycles_WithFiniteRowsAndColumns_AreInfeasible()
    {
        int?[,] matrix =
            { { null, 1, null, null }, { 1, null, null, null }, { null, null, null, 1 }, { null, null, 1, null } };
        var session = Create(Problem(matrix));
        Finish(session);
        Assert.Equal(SearchStatus.Infeasible, session.Result.Status);
        Assert.Null(session.Result.BestRoute);
    }

    [Fact]
    public void TeachingExample_ProducesExpectedBoundsRoutesAndBranches()
    {
        var problem = ProblemSamples.TeachingExample();
        var session = new LittleSolver().CreateSession(problem);
        Assert.Equal(36, session.Result.LowerBound);
        session.Advance();
        var root = Assert.Single(session.Steps);
        Assert.Equal(new long?[] { 10, 2, 2, 3, 4, 7 }, root.Reduction.RowMinima);
        Assert.Equal(new long?[] { 1, 0, 2, 0, 0, 5 }, root.Reduction.ColumnMinima);
        Assert.Equal(2, root.Children.Count);
        Assert.All(root.Children, child => Assert.Equal(root.NodeId, child.ParentId));
        Finish(session);
        Assert.Equal(SearchStatus.Optimal, session.Result.Status);
        Assert.Equal(39, session.Result.BestRoute!.Cost);
        Assert.Equal(new[] { 0, 4, 1, 3, 5, 2, 0 }, session.Result.BestRoute.Vertices);
        Assert.Equal(39, session.Result.LowerBound);
        Assert.Equal(0d, session.Result.RelativeGapPercent);
    }

    [Fact]
    public void CheapThreeVertexSubtours_MustNotBecomeTheFinalRoute()
    {
        var matrix = new int?[6, 6];
        for (var i = 0; i < 6; i++)
        for (var j = 0; j < 6; j++)
            matrix[i, j] = i == j ? null : 20;
        foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 0), (3, 4), (4, 5), (5, 3) })
            matrix[a, b] = 1;
        var session = Create(Problem(matrix));
        Finish(session);
        Assert.Equal(SearchStatus.Optimal, session.Result.Status);
        Assert.Equal(44, session.Result.BestRoute!.Cost);
        Assert.Equal(6, session.Result.BestRoute.Vertices.SkipLast(1).Distinct().Count());
    }

    [Fact]
    public void SparseDirectedGraph_FindsOnlyValidCycle()
    {
        int?[,] matrix = { { null, 1, 2, null }, { null, null, 1, 2 }, { null, 2, null, 1 }, { 1, null, null, null } };
        matrix[3, 0] = null;
        matrix[3, 2] = 1;
        matrix[2, 0] = 1;
        var session = Create(Problem(matrix));
        Assert.Null(session.Result.BestRoute);
        Finish(session);
        Assert.Equal(SearchStatus.Optimal, session.Result.Status);
        Assert.Equal(new[] { 0, 1, 3, 2, 0 }, session.Result.BestRoute!.Vertices);
        Assert.Equal(5, session.Result.BestRoute.Cost);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void EqualCostsAndTies_AreDeterministicAndAllowZero(int cost)
    {
        var matrix = new int?[4, 4];
        for (var i = 0; i < 4; i++)
        for (var j = 0; j < 4; j++)
            matrix[i, j] = i == j ? null : cost;
        var problem = Problem(matrix, 2);
        var first = Create(problem);
        var second = Create(problem);
        Finish(first);
        Finish(second);
        Assert.Equal(SearchStatus.Optimal, first.Result.Status);
        Assert.Equal(cost * 4L, first.Result.BestRoute!.Cost);
        Assert.Equal(2, first.Result.BestRoute.Vertices[0]);
        Assert.Equal(2, first.Result.BestRoute.Vertices[^1]);
        Assert.Equal(first.Result.BestRoute.Vertices, second.Result.BestRoute!.Vertices);
        Assert.Equal(0, first.Result.AbsoluteGap);
        if (cost == 0) Assert.Null(first.Result.RelativeGapPercent);
    }

    [Fact]
    public void ForbiddenTransitionsWithoutHamiltonianCycle_AreInfeasible()
    {
        int?[,] matrix = { { null, 1, null }, { 1, null, null }, { null, null, null } };
        var session = Create(Problem(matrix));
        Finish(session);
        Assert.Equal(SearchStatus.Infeasible, session.Result.Status);
        Assert.Null(session.Result.BestRoute);
    }

    [Fact]
    public void StopAndRestart_PreserveBoundsAndDoNotShareState()
    {
        var problem = ProblemSamples.TeachingExample();
        var stopped = Create(problem);
        stopped.Advance(new CancellationToken(true));
        Assert.Equal(SearchStatus.Stopped, stopped.Result.Status);
        Assert.Equal(StopReason.Cancelled, stopped.Result.StopReason);
        Assert.Equal(36, stopped.Result.LowerBound);
        Assert.Null(stopped.Result.BestRoute);
        stopped.Advance();
        Assert.Empty(stopped.Steps);
        var fresh = Create(problem);
        Finish(fresh);
        Assert.Equal(SearchStatus.Optimal, fresh.Result.Status);
        Assert.Equal(39, fresh.Result.BestRoute!.Cost);
    }

    [Fact]
    public void TraceLimit_DoesNotLimitSearchOrExposeMutableMatrices()
    {
        var session = Create(ProblemSamples.TeachingExample(), new SearchOptions(MaxDetailedSteps: 1));
        session.Advance();
        var first = session.Steps[0].Reduction.Reduced[0, 1];
        Finish(session);
        Assert.Single(session.Steps);
        Assert.True(session.Result.TraceTruncated);
        Assert.Equal(39, session.Result.BestRoute!.Cost);
        Assert.Equal(first, session.Steps[0].Reduction.Reduced[0, 1]);
    }

    [Fact]
    public void BatchesAndSingleSteps_ReturnIdenticalResults()
    {
        var single = Create(ProblemSamples.TeachingExample());
        var batches = Create(ProblemSamples.TeachingExample());
        Finish(single);
        while (batches.Result.Status == SearchStatus.Running)
            for (var i = 0; i < 5 && batches.Result.Status == SearchStatus.Running; i++)
                batches.Advance();
        Assert.Equal(single.Result.BestRoute!.Vertices, batches.Result.BestRoute!.Vertices);
        Assert.Equal(single.Result.Statistics.Processed, batches.Result.Statistics.Processed);
        Assert.Equal(single.Steps.Select(s => s.Branch), batches.Steps.Select(s => s.Branch));
    }

    internal static void Finish(ISearchSession session)
    {
        for (var i = 0; i < 100_000 && session.Result.Status == SearchStatus.Running; i++) session.Advance();
        Assert.NotEqual(SearchStatus.Running, session.Result.Status);
    }

    internal static TspProblem Problem(int?[,] matrix, int start = 0) =>
        new(Enumerable.Range(1, matrix.GetLength(0)).Select(i => $"Object {i}"), matrix, start);

    private static ISearchSession Create(TspProblem problem, SearchOptions? options = null) =>
        new LittleSolver().CreateSession(problem, options);
}