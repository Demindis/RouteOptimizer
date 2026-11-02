namespace RouteOptimizer.Domain;

public sealed class Route
{
    public Route(TspProblem problem, IEnumerable<int> vertices)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(vertices);
        var path = vertices.ToArray();
        if (path.Length != problem.Count + 1 || path[0] != problem.Start || path[^1] != problem.Start ||
            path.SkipLast(1).Any(v => v < 0 || v >= problem.Count) ||
            path.SkipLast(1).Distinct().Count() != problem.Count)
            throw new ArgumentException("Маршрут должен посетить каждый объект один раз и вернуться в начало.", nameof(vertices));
        long cost = 0;
        for (var i = 0; i < problem.Count; i++)
            cost += problem[path[i], path[i + 1]] ?? throw new ArgumentException("Маршрут содержит запрещённый переход.", nameof(vertices));
        Vertices = Array.AsReadOnly(path);
        Cost = cost;
    }

    public IReadOnlyList<int> Vertices { get; }
    public long Cost { get; }
}
