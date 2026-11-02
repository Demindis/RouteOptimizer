using RouteOptimizer.Domain;

namespace RouteOptimizer.Application;

public enum SearchStatus
{
    Running,
    Optimal,
    Infeasible,
    Stopped,
}

public enum StopReason
{
    None,
    Cancelled,
    TimeLimit,
    OpenNodeLimit,
}

public enum NodeState
{
    Open,
    Expanded,
    PrunedByBound,
    Infeasible,
    Complete,
}

public enum BranchKind
{
    Root,
    Include,
    Exclude,
}

public readonly record struct Arc(int From, int To);

public sealed record ZeroPenalty(Arc Arc, long? Penalty);

public sealed record SearchOptions(
    TimeSpan? ActiveTimeLimit = null,
    int MaxOpenNodes = 20_000,
    int MaxDetailedSteps = 2_000
)
{
    public TimeSpan TimeLimit => ActiveTimeLimit ?? TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (TimeLimit <= TimeSpan.Zero || MaxOpenNodes < 1 || MaxDetailedSteps < 0)
            throw new ArgumentOutOfRangeException(
                nameof(SearchOptions),
                "Ограничения поиска должны быть положительными; размер журнала может быть нулевым."
            );
    }
}

// Only indexed reads are exposed; mutable solver arrays never cross the application boundary.
public sealed class MatrixSnapshot
{
    private readonly long?[,] _values;

    public MatrixSnapshot(long?[,] values, IEnumerable<int> rows, IEnumerable<int> columns)
    {
        _values = (long?[,])values.Clone();
        Rows = Array.AsReadOnly(rows.ToArray());
        Columns = Array.AsReadOnly(columns.ToArray());
    }

    public IReadOnlyList<int> Rows { get; }
    public IReadOnlyList<int> Columns { get; }
    public long? this[int row, int column] => _values[Rows[row], Columns[column]];
}

public sealed record ReductionSnapshot(
    MatrixSnapshot Before,
    MatrixSnapshot AfterRows,
    MatrixSnapshot Reduced,
    IReadOnlyList<long?> RowMinima,
    IReadOnlyList<long?> ColumnMinima,
    long AddedBound,
    bool Feasible
);

public sealed record BranchOutcome(
    int NodeId,
    int ParentId,
    BranchKind Kind,
    Arc Arc,
    long LowerBound,
    NodeState State,
    ReductionSnapshot Reduction,
    IReadOnlyList<Arc> SubtourProhibitions
);

public sealed record SearchSnapshot(
    int NodeId,
    int? ParentId,
    BranchKind Kind,
    NodeState State,
    long LowerBound,
    ReductionSnapshot Reduction,
    IReadOnlyList<ZeroPenalty> Penalties,
    Arc? Branch,
    IReadOnlyList<Arc> IncludedArcs,
    IReadOnlyList<BranchOutcome> Children
);

public sealed record SearchStatistics(
    int Created,
    int Processed,
    int PrunedByBound,
    int Infeasible,
    int Open,
    int MaxOpen,
    TimeSpan Elapsed
);

public sealed record SolveResult(
    SearchStatus Status,
    StopReason StopReason,
    Route? BestRoute,
    long? LowerBound,
    SearchStatistics Statistics,
    bool TraceTruncated
)
{
    public long? AbsoluteGap =>
        BestRoute is { } route && LowerBound is { } lower ? route.Cost - lower : null;

    public double? RelativeGapPercent =>
        BestRoute is { Cost: > 0 } route && AbsoluteGap is { } gap ? gap * 100d / route.Cost : null;
}

public interface ILittleSolver
{
    ISearchSession CreateSession(TspProblem problem, SearchOptions? options = null);
}

public interface ISearchSession
{
    SolveResult Result { get; }
    IReadOnlyList<SearchSnapshot> Steps { get; }
    void Advance(CancellationToken cancellationToken = default);
    void Stop();
}
