using System.Globalization;
using ClosedXML.Excel;
using RouteOptimizer.Application;
using RouteOptimizer.Domain;

namespace RouteOptimizer.Infrastructure;

public sealed class ExcelProblemWorkbook : IProblemWorkbook
{
    public TspProblem Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);

        // Шаблон из Книга1.xlsx содержит только один лист «Лист1».
        // Справа на этом же листе расположены инструкции (I:J),
        // поэтому НЕ используем LastColumnUsed() для определения размера матрицы.
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("Excel-файл не содержит листов.");

        string Text(int row, int col)
        {
            var cell = sheet.Cell(row, col);
            if (cell.HasFormula)
                throw new ArgumentException($"{cell.Address}: формулы не поддерживаются. Вставьте значения.");
            return cell.GetString().Trim();
        }

        // Считаем только непрерывные названия объектов в столбце A,
        // начиная с A2. Инструкция справа не влияет на размер матрицы.
        var n = 0;
        for (var row = 2; row <= 12; row++)
        {
            if (string.IsNullOrWhiteSpace(Text(row, 1)))
                break;
            n++;
        }

        if (n is < 3 or > 10)
            throw new ArgumentException(
                "Нужна квадратная матрица 3–10 объектов: названия в первой строке и столбце A.");

        var names = new string[n];
        var costs = new int?[n, n];

        for (var i = 0; i < n; i++)
        {
            names[i] = Text(1, i + 2);
            if (string.IsNullOrWhiteSpace(names[i]))
                throw new ArgumentException(
                    $"{sheet.Cell(1, i + 2).Address}: укажите название объекта.");

            if (Text(i + 2, 1) != names[i])
                throw new ArgumentException(
                    $"A{i + 2}: название должно совпадать с заголовком {sheet.Cell(1, i + 2).Address}.");

            for (var j = 0; j < n; j++)
            {
                // Переход объекта к самому себе запрещён независимо от содержимого диагонали.
                if (i == j)
                    continue;

                var value = Text(i + 2, j + 2);
                if (value is "∞" or "-")
                    continue;

                if (!int.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var cost) || cost < 0)
                    throw new ArgumentException(
                        $"{sheet.Cell(i + 2, j + 2).Address}: стоимость должна быть " +
                        $"целым числом от 0 до {int.MaxValue}; запрет — ∞ или «-». " +
                        "Пустые ячейки заполните.");

                costs[i, j] = cost;
            }
        }

        return new TspProblem(names, costs);
    }

    public byte[] CreateTemplate()
    {
        // Возвращаем точный оригинальный Excel-образец: один лист,
        // стандартная сетка, чёрные границы, серая диагональ,
        // инструкция справа, а не на втором листе.
        using var stream = typeof(ExcelProblemWorkbook).Assembly
            .GetManifestResourceStream("RouteOptimizer.Template.xlsx")
            ?? throw new InvalidOperationException(
                "Не найден встроенный шаблон RouteOptimizer.Template.xlsx. " +
                "Добавьте Resources/RouteOptimizerTemplate.xlsx как EmbeddedResource в .csproj.");

        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
