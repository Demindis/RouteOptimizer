using RouteOptimizer.Domain;

namespace RouteOptimizer.Application;

public static class ProblemSamples
{
    public static TspProblem TeachingExample() =>
        new(
            new[]
            {
                "Сервисный центр",
                "Административный корпус",
                "Учебный корпус",
                "Библиотека",
                "Лаборатория",
                "Склад",
            },
            new int?[,]
            {
                { null, 20, 30, 10, 11, 25 },
                { 15, null, 16, 4, 2, 18 },
                { 3, 5, null, 2, 4, 14 },
                { 19, 6, 18, null, 3, 8 },
                { 16, 4, 7, 16, null, 12 },
                { 22, 10, 9, 7, 13, null },
            }
        );

    // Explicit PRNG makes experiment inputs reproducible independently of System.Random versions.
    public static TspProblem Experiment(int size, uint seed = 2026)
    {
        if (size is < 3 or > 10)
            throw new ArgumentOutOfRangeException(nameof(size));
        var matrix = new int?[size, size];
        for (var i = 0; i < size; i++)
        for (var j = 0; j < size; j++)
        {
            if (i == j)
                continue;
            seed = unchecked(seed * 1664525 + 1013904223);
            matrix[i, j] = (int)(seed % 60) + 1;
        }
        return new TspProblem(Enumerable.Range(1, size).Select(i => $"Объект {i}"), matrix);
    }
}
