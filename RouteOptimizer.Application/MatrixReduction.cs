namespace RouteOptimizer.Application;

internal static class MatrixReduction
{
    public static ReductionSnapshot Reduce(long?[,] matrix, int[] rows, int[] columns)
    {
        var before = new MatrixSnapshot(matrix, rows, columns);
        var rowMinima = rows.Select(i => columns.Select(j => matrix[i, j]).Min()).ToArray();
        long added = 0;
        for (var r = 0; r < rows.Length; r++)
        {
            if (rowMinima[r] is not { } min) continue;
            added += min;
            foreach (var j in columns) if (matrix[rows[r], j].HasValue) matrix[rows[r], j] -= min;
        }
        var afterRows = new MatrixSnapshot(matrix, rows, columns);
        var columnMinima = columns.Select(j => rows.Select(i => matrix[i, j]).Min()).ToArray();
        for (var c = 0; c < columns.Length; c++)
        {
            if (columnMinima[c] is not { } min) continue;
            added += min;
            foreach (var i in rows) if (matrix[i, columns[c]].HasValue) matrix[i, columns[c]] -= min;
        }
        return new ReductionSnapshot(before, afterRows, new MatrixSnapshot(matrix, rows, columns),
            Array.AsReadOnly(rowMinima), Array.AsReadOnly(columnMinima), added,
            rowMinima.All(m => m.HasValue) && columnMinima.All(m => m.HasValue));
    }

    public static IReadOnlyList<ZeroPenalty> Penalties(long?[,] matrix, int[] rows, int[] columns)
    {
        var result = new List<ZeroPenalty>();
        foreach (var i in rows)
        foreach (var j in columns)
        {
            if (matrix[i, j] != 0) continue;
            var row = columns.Where(k => k != j).Select(k => matrix[i, k]).Min();
            var col = rows.Where(k => k != i).Select(k => matrix[k, j]).Min();
            result.Add(new ZeroPenalty(new Arc(i, j), row + col));
        }
        return result.AsReadOnly();
    }
}
