using RouteOptimizer.Application;

namespace RouteOptimizer.Models;

public static class DisplayText
{
    private static string Count(int value, string one, string few, string many) =>
        $"{value} {(value % 100 is >= 11 and <= 14 ? many : value % 10 == 1 ? one : value % 10 is >= 2 and <= 4 ? few : many)}";

    public static string NodeCount(int value) => Count(value, "узел", "узла", "узлов");

    public static string ObjectCount(int value) => Count(value, "объект", "объекта", "объектов");

    public static string Status(SearchStatus s) =>
        s switch
        {
            SearchStatus.Running => "Поиск выполняется",
            SearchStatus.Optimal => "Оптимум доказан",
            SearchStatus.Infeasible => "Допустимого маршрута нет",
            _ => "Поиск остановлен",
        };

    public static string State(NodeState s) =>
        s switch
        {
            NodeState.Open => "Открыт",
            NodeState.Expanded => "Ветвление",
            NodeState.PrunedByBound => "Отсечён по оценке",
            NodeState.Infeasible => "Недопустим",
            _ => "Маршрут найден",
        };

    public static string Kind(BranchKind k) =>
        k switch
        {
            BranchKind.Include => "Включить",
            BranchKind.Exclude => "Исключить",
            _ => "Исходная задача",
        };

    public static string Arc(Arc a) => $"{a.From + 1} → {a.To + 1}";

    public static string Value(long? v) => v?.ToString() ?? "∞";

    public static string Stop(StopReason r) =>
        r switch
        {
            StopReason.TimeLimit => "Достигнут лимит активного времени.",
            StopReason.OpenNodeLimit =>
                "Достигнут лимит открытых узлов. Нижняя оценка учитывает неисследованную область.",
            StopReason.Cancelled => "Поиск остановлен пользователем.",
            _ => "",
        };
}
