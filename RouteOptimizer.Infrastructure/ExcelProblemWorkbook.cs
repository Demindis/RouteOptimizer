using System.Globalization;
using ClosedXML.Excel;
using RouteOptimizer.Application;
using RouteOptimizer.Domain;

namespace RouteOptimizer.Infrastructure;

/// <summary>
/// Реализует импорт задачи коммивояжёра из Excel
/// и формирование шаблона матрицы стоимостей.
/// </summary>
/// <remarks>
/// Использует библиотеку ClosedXML для работы с файлами XLSX.
///
/// Структура листа:
/// - A1 — заголовок матрицы;
/// - B1 и далее — названия объектов;
/// - A2 и далее — названия объектов;
/// - B2 и далее — стоимости переходов;
/// - I1:J3 — инструкция по заполнению шаблона.
///
/// Поддерживается от 3 до 10 объектов.
/// </remarks>
public sealed class ExcelProblemWorkbook : IProblemWorkbook
{
    /// <summary>
    /// Считывает матрицу стоимостей из Excel-файла
    /// и преобразует её в задачу коммивояжёра.
    /// </summary>
    /// <param name="stream">
    /// Поток, содержащий XLSX-файл с матрицей стоимостей.
    /// </param>
    /// <returns>
    /// Экземпляр <see cref="TspProblem"/>,
    /// содержащий названия объектов и матрицу переходов.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Возникает при некорректной структуре матрицы,
    /// несовпадении названий, наличии формул
    /// или недопустимых значений стоимости.
    /// </exception>
    public TspProblem Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);

        // Используем лист «Матрица» либо первый лист файла.
        // Это обеспечивает совместимость со старыми шаблонами.
        var sheet =
            workbook.Worksheets.FirstOrDefault(x => x.Name == "Лист1")
            ?? throw new ArgumentException(
                "Не найден лист «Лист1». Используйте Excel-шаблон RouteOptimizer."
            );

        // Получает текстовое значение ячейки.
        // Формулы запрещены: матрица должна содержать
        // только непосредственно введённые значения.
        string Text(int row, int column)
        {
            var cell = sheet.Cell(row, column);

            if (cell.HasFormula)
            {
                throw new ArgumentException(
                    $"{cell.Address}: формулы не поддерживаются. " + "Вставьте значения."
                );
            }

            return cell.GetString().Trim();
        }

        // Определяем количество объектов по названиям
        // в первом столбце, начиная с ячейки A2.
        //
        // Инструкция справа (I1:J3) не учитывается
        // при определении размера матрицы.
        var count = 0;

        for (var row = 2; row <= 12; row++)
        {
            if (string.IsNullOrWhiteSpace(Text(row, 1)))
                break;

            count++;
        }

        // Алгоритм поддерживает от 3 до 10 объектов.
        if (count is < 3 or > 10)
        {
            throw new ArgumentException(
                "Нужна квадратная матрица 3–10 объектов "
                    + "с названиями в первой строке и столбце A."
            );
        }

        // Массив названий объектов и матрица стоимостей.
        //
        // null обозначает запрещённый переход.
        var names = new string[count];
        var costs = new int?[count, count];

        for (var i = 0; i < count; i++)
        {
            // Названия объектов берутся из первой строки.
            names[i] = Text(1, i + 2);

            if (string.IsNullOrWhiteSpace(names[i]))
            {
                throw new ArgumentException(
                    $"{sheet.Cell(1, i + 2).Address}: " + "укажите название объекта."
                );
            }

            // Название в первом столбце должно совпадать
            // с соответствующим заголовком первой строки.
            if (Text(i + 2, 1) != names[i])
            {
                throw new ArgumentException(
                    $"A{i + 2}: название должно совпадать "
                        + $"с заголовком {sheet.Cell(1, i + 2).Address}."
                );
            }

            for (var j = 0; j < count; j++)
            {
                // Диагональные переходы не рассматриваются:
                // объект не может переходить сам в себя.
                if (i == j)
                    continue;

                var value = Text(i + 2, j + 2);

                // Символы ∞ и - обозначают отсутствие дуги.
                // Соответствующий элемент остаётся null.
                if (value is "∞" or "-")
                    continue;

                // Стоимость должна быть целым
                // неотрицательным числом. Ноль допустим.
                if (
                    !int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var cost
                    )
                    || cost < 0
                )
                {
                    throw new ArgumentException(
                        $"{sheet.Cell(i + 2, j + 2).Address}: "
                            + "стоимость должна быть целым "
                            + $"числом от 0 до {int.MaxValue}; "
                            + "запрет — ∞ или «-». "
                            + "Пустые ячейки заполните."
                    );
                }

                costs[i, j] = cost;
            }
        }

        // Создаём задачу на основе считанных данных.
        return new TspProblem(names, costs);
    }

    /// <summary>
    /// Создаёт Excel-шаблон для ввода матрицы стоимостей.
    /// </summary>
    /// <returns>
    /// Массив байтов XLSX-файла, готовый для скачивания.
    /// </returns>
    /// <remarks>
    /// Шаблон формируется программно средствами ClosedXML.
    /// Готовые файлы Excel не используются.
    ///
    /// Создаётся один лист «Лист1», содержащий:
    /// - заполненную учебную матрицу;
    /// - серую диагональ запрещённых переходов;
    /// - тонкие границы ячеек;
    /// - инструкцию справа от матрицы.
    /// </remarks>
    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();

        // Создаём единственный лист Excel.
        var sheet = workbook.AddWorksheet("Лист1");

        // Получаем учебный пример задачи коммивояжёра.
        var problem = ProblemSamples.TeachingExample();

        // Дополнительная строка и столбец
        // используются для названий объектов.
        var lastRow = problem.Count + 1;
        var lastColumn = problem.Count + 1;

        // --------------------------------------------------
        // 1. Формирование матрицы стоимостей
        // --------------------------------------------------

        sheet.Cell(1, 1).Value = "Откуда / куда";

        for (var i = 0; i < problem.Count; i++)
        {
            // Заголовки строк и столбцов.
            sheet.Cell(1, i + 2).Value = problem.Names[i];
            sheet.Cell(i + 2, 1).Value = problem.Names[i];

            for (var j = 0; j < problem.Count; j++)
            {
                var cell = sheet.Cell(i + 2, j + 2);

                // Разрешённые переходы записываем числами,
                // запрещённые обозначаем символом бесконечности.
                if (problem[i, j] is { } cost)
                    cell.Value = cost;
                else
                    cell.Value = "∞";
            }
        }

        // Значение из исходного пользовательского образца.
        // Переход из сервисного центра
        // в административный корпус — 120 минут.
        sheet.Cell("C2").Value = 120;

        // --------------------------------------------------
        // 2. Основное оформление матрицы
        // --------------------------------------------------

        var matrix = sheet.Range(1, 1, lastRow, lastColumn);

        // Настраиваем единый шрифт таблицы.
        matrix.Style.Font.FontName = "Times New Roman";
        matrix.Style.Font.FontSize = 12;

        // Добавляем тонкие внутренние и внешние границы.
        matrix.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        matrix.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        matrix.Style.Border.InsideBorderColor = XLColor.Black;

        matrix.Style.Border.OutsideBorderColor = XLColor.Black;

        // Выделяем названия столбцов полужирным шрифтом.
        sheet.Range(1, 1, 1, lastColumn).Style.Font.Bold = true;

        // --------------------------------------------------
        // 3. Ширина столбцов
        // --------------------------------------------------

        // Первый столбец шире, поскольку содержит
        // полные названия объектов.
        sheet.Column(1).Width = 26.89;

        // Остальные столбцы имеют одинаковую ширину.
        sheet.Columns(2, lastColumn).Width = 18;

        // --------------------------------------------------
        // 4. Выделение диагональных элементов
        // --------------------------------------------------

        // Диагональ обозначает переход объекта
        // в самого себя. Такие переходы запрещены.
        //
        // Для наглядности выделяем соответствующие
        // ячейки серой заливкой.
        for (var i = 0; i < problem.Count; i++)
        {
            sheet.Cell(i + 2, i + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#808080");
        }

        // --------------------------------------------------
        // 5. Инструкция по заполнению
        // --------------------------------------------------

        // Инструкция располагается справа от матрицы
        // на том же листе. Объединение ячеек не требуется.
        sheet.Cell("I1").Value = "Инструкция:";

        sheet.Cell("J1").Value =
            "На этом листе измените названия одинаково "
            + "в строке 1 и столбце A. Размер: 3–10 объектов.";

        sheet.Cell("J2").Value =
            "Стоимость — целое неотрицательное число. "
            + "0 допустим. Запрет: ∞ или -. "
            + "Диагональ игнорируется. "
            + "Формулы не поддерживаются.";

        sheet.Cell("J3").Value =
            "Шаблон заполнен учебным примером. "
            + "Начальный объект выбирается на сайте "
            + "после загрузки.";

        // Настраиваем ширину столбца с заголовком инструкции.
        sheet.Column(9).Width = 18.44;

        // Выделяем название инструкции.
        sheet.Cell("I1").Style.Font.Bold = true;

        // Применяем единый шрифт к инструкции.
        foreach (var address in new[] { "I1", "J1", "J2", "J3" })
        {
            var cell = sheet.Cell(address);

            cell.Style.Font.FontName = "Times New Roman";
            cell.Style.Font.FontSize = 12;
        }

        // --------------------------------------------------
        // 6. Настройки отображения Excel
        // --------------------------------------------------

        // Оставляем стандартную сетку Excel видимой
        // как внутри, так и за пределами матрицы.
        sheet.ShowGridLines = true;

        // --------------------------------------------------
        // 7. Сохранение сформированного файла
        // --------------------------------------------------

        // Записываем книгу в поток памяти,
        // не создавая временный файл на диске.
        using var output = new MemoryStream();

        workbook.SaveAs(output);

        // Возвращаем содержимое XLSX для скачивания.
        return output.ToArray();
    }
}
