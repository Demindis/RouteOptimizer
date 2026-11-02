using RouteOptimizer.Domain;

namespace RouteOptimizer.Models;

public sealed class ProblemDraft
{
    public int Size => Names.Length;
    public int Start { get; set; }
    public string[] Names { get; private init; } = [];
    public string[,] Costs { get; private init; } = new string[0, 0];
    public bool[,] Forbidden { get; private init; } = new bool[0, 0];

    public static ProblemDraft From(TspProblem problem)
    {
        var draft = new ProblemDraft
        {
            Names = problem.Names.ToArray(),
            Start = problem.Start,
            Costs = new string[problem.Count, problem.Count],
            Forbidden = new bool[problem.Count, problem.Count],
        };
        for (var i = 0; i < problem.Count; i++)
        for (var j = 0; j < problem.Count; j++)
        {
            draft.Costs[i, j] = (problem[i, j] ?? 0).ToString();
            draft.Forbidden[i, j] = !problem[i, j].HasValue;
        }
        return draft;
    }

    public ProblemDraft Copy() =>
        new()
        {
            Names = (string[])Names.Clone(),
            Costs = (string[,])Costs.Clone(),
            Forbidden = (bool[,])Forbidden.Clone(),
            Start = Start,
        };

    public ProblemDraft Resize(int size)
    {
        var matrix = new int?[size, size];
        for (var i = 0; i < size; i++)
        for (var j = 0; j < size; j++)
            matrix[i, j] = i == j ? null : 10;
        var draft = From(
            new TspProblem(Enumerable.Range(1, size).Select(i => $"Объект {i}"), matrix)
        );
        draft.Start = Math.Min(Start, size - 1);
        for (var i = 0; i < Math.Min(size, Size); i++)
        {
            draft.Names[i] = Names[i];
            for (var j = 0; j < Math.Min(size, Size); j++)
            {
                draft.Costs[i, j] = Costs[i, j];
                draft.Forbidden[i, j] = Forbidden[i, j];
            }
        }
        return draft;
    }

    public string? CellError(int row, int column) =>
        Forbidden[row, column]
        || row == column
        || int.TryParse(Costs[row, column], out var value) && value >= 0
            ? null
            : $"Переход {row + 1} → {column + 1}: введите целое число от 0 до {int.MaxValue} или ∞.";

    public TspProblem Build()
    {
        for (var i = 0; i < Size; i++)
            if (string.IsNullOrWhiteSpace(Names[i]))
                throw new ArgumentException($"Укажите название объекта {i + 1}.");
        var matrix = new int?[Size, Size];
        for (var i = 0; i < Size; i++)
        for (var j = 0; j < Size; j++)
        {
            if (CellError(i, j) is { } error)
                throw new ArgumentException(error);
            if (!Forbidden[i, j] && i != j)
                matrix[i, j] = int.Parse(Costs[i, j]);
        }
        return new TspProblem(Names, matrix, Start);
    }
}
