using RouteOptimizer.Domain;

namespace RouteOptimizer.Application;

internal sealed class LittleSearchSession : ISearchSession
{
    private sealed record Node(
        int Id,
        int? ParentId,
        BranchKind Kind,
        long?[,] Matrix,
        int[] Rows,
        int[] Columns,
        int[] Next,
        long Bound,
        ReductionSnapshot Reduction);

    private readonly TspProblem _problem;
    private readonly SearchOptions _options;
    private readonly TimeProvider _clock;
    private readonly PriorityQueue<Node, (long Bound, int Id)> _open = new();
    private readonly List<SearchSnapshot> _steps = [];
    private Route? _best;
    private SearchStatus _status = SearchStatus.Running;
    private StopReason _reason;
    private int _created, _processed, _pruned, _infeasible, _maxOpen;
    private TimeSpan _elapsed;

    public LittleSearchSession(TspProblem problem, SearchOptions options, TimeProvider clock)
    {
        _problem = problem;
        _options = options;
        _clock = clock;
        var start = _clock.GetTimestamp();
        var matrix = new long?[problem.Count, problem.Count];
        for (var i = 0; i < problem.Count; i++)
        for (var j = 0; j < problem.Count; j++)
            matrix[i, j] = problem[i, j];
        var vertices = Enumerable.Range(0, problem.Count).ToArray();
        var reduction = MatrixReduction.Reduce(matrix, vertices, vertices);
        var root = new Node(++_created, null, BranchKind.Root, matrix, vertices, vertices,
            Enumerable.Repeat(-1, problem.Count).ToArray(), reduction.AddedBound, reduction);
        // Keep even an infeasible root for a visible explanation on the first step.
        Enqueue(root);
        Steps = _steps.AsReadOnly();
        _elapsed = _clock.GetElapsedTime(start);
    }

    public IReadOnlyList<SearchSnapshot> Steps { get; }

    public SolveResult Result
    {
        get
        {
            long? lower = _open.TryPeek(out var node, out _) ? node.Bound : _best?.Cost;
            if (_best is { } best && lower is { } bound) lower = Math.Min(bound, best.Cost);
            return new SolveResult(_status, _reason, _best, lower,
                new SearchStatistics(_created, _processed, _pruned, _infeasible, _open.Count, _maxOpen, _elapsed),
                _processed > _options.MaxDetailedSteps);
        }
    }

    public void Stop()
    {
        if (_status != SearchStatus.Running) return;
        _status = SearchStatus.Stopped;
        _reason = StopReason.Cancelled;
    }

    public void Advance(CancellationToken cancellationToken = default)
    {
        if (_status != SearchStatus.Running) return;
        if (cancellationToken.IsCancellationRequested)
        {
            Stop();
            return;
        }

        if (_elapsed >= _options.TimeLimit)
        {
            StopAt(StopReason.TimeLimit);
            return;
        }

        var start = _clock.GetTimestamp();
        var node = _open.Dequeue();
        _processed++;
        var state = Classify(node);
        IReadOnlyList<ZeroPenalty> penalties = Array.Empty<ZeroPenalty>();
        Arc? branch = null;
        var outcomes = new List<BranchOutcome>();
        if (state == NodeState.Open)
        {
            penalties = MatrixReduction.Penalties(node.Matrix, node.Rows, node.Columns);
            // A missing alternative represents an infinite penalty.
            branch = penalties.OrderByDescending(p => !p.Penalty.HasValue)
                .ThenByDescending(p => p.Penalty).ThenBy(p => p.Arc.From).ThenBy(p => p.Arc.To).First().Arc;
            var children = new List<Node>();
            foreach (var kind in new[] { BranchKind.Include, BranchKind.Exclude })
            {
                var (child, forbidden) = MakeChild(node, branch.Value, kind);
                var childState = Classify(child);
                if (childState == NodeState.Open) children.Add(child);
                outcomes.Add(new BranchOutcome(child.Id, node.Id, kind, branch.Value, child.Bound,
                    childState, child.Reduction, forbidden));
            }

            if (_open.Count + children.Count > _options.MaxOpenNodes)
            {
                // Retain the unexplored parent, keeping the global bound valid on interruption.
                Enqueue(node);
                StopAt(StopReason.OpenNodeLimit);
                state = NodeState.Open;
            }
            else
            {
                foreach (var child in children) Enqueue(child);
                state = NodeState.Expanded;
            }
        }

        if (_steps.Count < _options.MaxDetailedSteps)
            _steps.Add(new SearchSnapshot(node.Id, node.ParentId, node.Kind, state, node.Bound,
                node.Reduction, penalties, branch, Included(node.Next), outcomes.AsReadOnly()));
        _elapsed += _clock.GetElapsedTime(start);
        if (_open.Count == 0)
            _status = _best is null ? SearchStatus.Infeasible : SearchStatus.Optimal;
        else if (_status == SearchStatus.Running && _elapsed >= _options.TimeLimit) StopAt(StopReason.TimeLimit);
    }

    private NodeState Classify(Node node)
    {
        if (!node.Reduction.Feasible)
        {
            _infeasible++;
            return NodeState.Infeasible;
        }

        if (node.Rows.Length == 0)
        {
            var path = new List<int> { _problem.Start };
            for (var i = 0; i < _problem.Count; i++) path.Add(node.Next[path[^1]]);
            var route = new Route(_problem, path);
            if (_best is null || route.Cost < _best.Cost) _best = route;
            return NodeState.Complete;
        }

        if (_best is not null && node.Bound >= _best.Cost)
        {
            _pruned++;
            return NodeState.PrunedByBound;
        }

        return NodeState.Open;
    }

    private (Node Node, IReadOnlyList<Arc> Forbidden) MakeChild(Node parent, Arc arc, BranchKind kind)
    {
        var matrix = (long?[,])parent.Matrix.Clone();
        var next = (int[])parent.Next.Clone();
        var rows = parent.Rows;
        var columns = parent.Columns;
        var forbidden = new List<Arc>();
        if (kind == BranchKind.Exclude) matrix[arc.From, arc.To] = null;
        else
        {
            next[arc.From] = arc.To;
            rows = rows.Where(i => i != arc.From).ToArray();
            columns = columns.Where(j => j != arc.To).ToArray();
            if (rows.Length > 0)
            {
                // Every selected component is a directed path. Forbid end -> start for
                // each proper path, including paths formed by merging earlier chains.
                var incoming = next.Where(v => v >= 0).ToHashSet();
                foreach (var head in Enumerable.Range(0, next.Length).Where(v => !incoming.Contains(v)))
                {
                    var tail = head;
                    var length = 1;
                    while (next[tail] >= 0 && length <= next.Length)
                    {
                        tail = next[tail];
                        length++;
                    }

                    if (length < next.Length && matrix[tail, head].HasValue)
                    {
                        matrix[tail, head] = null;
                        forbidden.Add(new Arc(tail, head));
                    }
                }
            }
        }

        var reduction = MatrixReduction.Reduce(matrix, rows, columns);
        return (new Node(++_created, parent.Id, kind, matrix, rows, columns, next,
            parent.Bound + reduction.AddedBound, reduction), forbidden.AsReadOnly());
    }

    private void Enqueue(Node node)
    {
        _open.Enqueue(node, (node.Bound, node.Id));
        _maxOpen = Math.Max(_maxOpen, _open.Count);
    }

    private void StopAt(StopReason reason)
    {
        _status = SearchStatus.Stopped;
        _reason = reason;
    }

    private static IReadOnlyList<Arc> Included(int[] next) => Array.AsReadOnly(
        Enumerable.Range(0, next.Length).Where(i => next[i] >= 0).Select(i => new Arc(i, next[i])).ToArray());
}