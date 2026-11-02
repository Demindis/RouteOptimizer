using RouteOptimizer.Application;
using RouteOptimizer.Domain;

namespace RouteOptimizer.Tests;

public sealed class DomainTests
{
    [Fact]
    public void ProblemAndRoute_DefensivelyCopyInputAndUseLongCost()
    {
        var names = new[] { "A", "B", "C" };
        int?[,] matrix = { { null, int.MaxValue, 1 }, { 1, null, int.MaxValue }, { int.MaxValue, 1, null } };
        var problem = new TspProblem(names, matrix);
        var path = new[] { 0, 1, 2, 0 };
        var route = new Route(problem, path);
        names[0] = "changed"; matrix[0, 1] = 0; path[1] = 0;
        var copy = problem.CopyCosts(); copy[0, 1] = 0;
        Assert.Equal("A", problem.Names[0]);
        Assert.Equal(int.MaxValue, problem[0, 1]);
        Assert.Equal(new[] { 0, 1, 2, 0 }, route.Vertices);
        Assert.Equal(3L * int.MaxValue, route.Cost);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(11)]
    public void Problem_RejectsUnsupportedSize(int size) => Assert.Throws<ArgumentException>(() =>
        new TspProblem(Enumerable.Range(0, size).Select(i => i.ToString()), new int?[size, size]));

    [Fact]
    public void Problem_RejectsInvalidShapeCostsNamesAndStart()
    {
        var names = new[] { "A", "B", "C" };
        Assert.Throws<ArgumentException>(() => new TspProblem(names, new int?[3, 4]));
        var matrix = new int?[3, 3]; matrix[0, 1] = -1;
        Assert.Throws<ArgumentException>(() => new TspProblem(names, matrix));
        matrix[0, 1] = 0; matrix[1, 1] = 0;
        Assert.Throws<ArgumentException>(() => new TspProblem(names, matrix));
        matrix[1, 1] = null;
        Assert.Throws<ArgumentException>(() => new TspProblem(new[] { "A", " ", "C" }, matrix));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TspProblem(names, matrix, 3));
    }

    [Theory]
    [InlineData(0, 1, 1, 0)]
    [InlineData(0, 1, 2, 1)]
    [InlineData(0, 1, 9, 0)]
    [InlineData(0, 2, 1, 0)]
    public void Route_RejectsRepeatedUnclosedInvalidOrForbiddenPath(int a, int b, int c, int d)
    {
        int?[,] matrix = { { null, 1, null }, { null, null, 1 }, { 1, null, null } };
        Assert.Throws<ArgumentException>(() => new Route(SolverTests.Problem(matrix), new[] { a, b, c, d }));
    }
}
