namespace RouteOptimizer.Domain;

public sealed class TspProblem
{
    private readonly int?[,] _costs;

    public TspProblem(IEnumerable<string> names, int?[,] costs, int start = 0)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(costs);
        var vertices = names.ToArray();
        if (vertices.Length is < 3 or > 10)
            throw new ArgumentException(
                "Количество объектов должно быть от 3 до 10.",
                nameof(names)
            );
        if (vertices.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Укажите название каждого объекта.", nameof(names));
        if (costs.GetLength(0) != vertices.Length || costs.GetLength(1) != vertices.Length)
            throw new ArgumentException(
                "Матрица должна быть квадратной и соответствовать числу объектов.",
                nameof(costs)
            );
        if (start < 0 || start >= vertices.Length)
            throw new ArgumentOutOfRangeException(nameof(start), "Выберите начальный объект.");
        _costs = (int?[,])costs.Clone();
        for (var i = 0; i < vertices.Length; i++)
        for (var j = 0; j < vertices.Length; j++)
        {
            if (_costs[i, j] < 0)
                throw new ArgumentException(
                    "Стоимость не может быть отрицательной.",
                    nameof(costs)
                );
            if (i == j && _costs[i, j].HasValue)
                throw new ArgumentException("Самопереходы должны быть запрещены.", nameof(costs));
        }
        Names = Array.AsReadOnly(vertices);
        Start = start;
    }

    public IReadOnlyList<string> Names { get; }
    public int Count => Names.Count;
    public int Start { get; }
    public int? this[int from, int to] => _costs[from, to];

    public int?[,] CopyCosts() => (int?[,])_costs.Clone();
}
