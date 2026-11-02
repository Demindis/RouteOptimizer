using RouteOptimizer.Domain;
namespace RouteOptimizer.Application;

public interface IProblemWorkbook
{
    TspProblem Read(Stream stream);
    byte[] CreateTemplate();
}
