using RouteOptimizer.Application;
using RouteOptimizer.Domain;

namespace RouteOptimizer.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void InnerAssemblies_ReferenceOnlyInwardAndFrameworkDependencies()
    {
        var domain = typeof(TspProblem)
            .Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();
        var application = typeof(LittleSolver)
            .Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();
        Assert.DoesNotContain(
            domain,
            name =>
                name!.StartsWith("RouteOptimizer")
                || name.StartsWith("MudBlazor")
                || name.StartsWith("Microsoft.AspNetCore")
        );
        Assert.Contains("RouteOptimizer.Domain", application);
        Assert.DoesNotContain(
            application,
            name =>
                name == "RouteOptimizer"
                || name!.StartsWith("MudBlazor")
                || name.StartsWith("Microsoft.AspNetCore")
        );
    }
}
