using RouteOptimizer.Domain;

namespace RouteOptimizer.Application;

public sealed class LittleSolver(TimeProvider? timeProvider = null) : ILittleSolver
{
    public ISearchSession CreateSession(TspProblem problem, SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var settings = options ?? new SearchOptions();
        settings.Validate();
        return new LittleSearchSession(problem, settings, timeProvider ?? TimeProvider.System);
    }
}
