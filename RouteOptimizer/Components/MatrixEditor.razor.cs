using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using RouteOptimizer.Models;

namespace RouteOptimizer.Components;

public partial class MatrixEditor
{
    [Parameter, EditorRequired] public ProblemDraft Draft { get; set; } = default!;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback<ProblemDraft> DraftChanged { get; set; }
    [Parameter] public EventCallback<string> LoadExample { get; set; }
    private readonly ElementReference[,] _inputs = new ElementReference[10, 10];
    private readonly ElementReference[] _nameInputs = new ElementReference[10];
    private int _row = -1, _column = -1;
    private string _sample = "teaching", _pasteText = "";
    private string? _pasteError;
    private bool _excelBusy, _excelFailed;
    private int _excelInputKey;
    private string? _excelMessage;
    private async Task DownloadTemplate()
    {
        _excelBusy = true; _excelMessage = null;
        try
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "./excel-download.js");
            try { await module.InvokeVoidAsync("download", Convert.ToBase64String(Workbook.CreateTemplate())); }
            finally { await module.DisposeAsync(); }
        }
        catch (Exception) { _excelFailed = true; _excelMessage = "Не удалось создать шаблон Excel. Попробуйте ещё раз."; }
        finally { _excelBusy = false; }
    }
    private async Task ImportExcel(InputFileChangeEventArgs e)
    {
        if (Disabled || _excelBusy || e.FileCount == 0) return;
        _excelBusy = true; _excelFailed = false; _excelMessage = null;
        try
        {
            if (!e.File.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Выберите файл .xlsx.");
            if (e.File.Size > 1024 * 1024) throw new ArgumentException("Размер файла не должен превышать 1 МБ.");
            await using var source = e.File.OpenReadStream(1024 * 1024);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer); buffer.Position = 0;
            var draft = ProblemDraft.From(Workbook.Read(buffer));
            draft.Start = Math.Min(Draft.Start, draft.Size - 1);
            _row = _column = -1;
            await DraftChanged.InvokeAsync(draft);
            _excelMessage = $"Загружено {draft.Size} объектов. Проверьте матрицу и выберите начало маршрута.";
        }
        catch (ArgumentException ex) { _excelFailed = true; _excelMessage = ex.Message; }
        catch (Exception) { _excelFailed = true; _excelMessage = "Не удалось прочитать Excel. Проверьте, что файл не повреждён и соответствует шаблону."; }
        finally { _excelBusy = false; _excelInputKey++; }
    }
    private string? FirstError
    {
        get
        {
            for (var i = 0; i < Draft.Size; i++)
            {
                if (string.IsNullOrWhiteSpace(Draft.Names[i])) return $"Укажите название объекта {i + 1}.";
                for (var j = 0; j < Draft.Size; j++) if (Draft.CellError(i, j) is { } error) return error;
            }
            return null;
        }
    }
    public async Task FocusFirstErrorAsync()
    {
        for (var i = 0; i < Draft.Size; i++)
        {
            if (string.IsNullOrWhiteSpace(Draft.Names[i])) { await _nameInputs[i].FocusAsync(); return; }
            for (var j = 0; j < Draft.Size; j++)
                if (Draft.CellError(i, j) is not null) { await _inputs[i, j].FocusAsync(); return; }
        }
    }
    private Task Resize(int size) { _row = _column = -1; return DraftChanged.InvokeAsync(Draft.Resize(size)); }
    private Task SetStart(int index) { var d = Draft.Copy(); d.Start = index; return DraftChanged.InvokeAsync(d); }
    private Task Rename(int index, string value) { var d = Draft.Copy(); d.Names[index] = value; return DraftChanged.InvokeAsync(d); }
    private Task SetCost(int row, int col, string value)
    {
        var d = Draft.Copy();
        d.Forbidden[row, col] = value.Trim() is "∞" or "-";
        if (!d.Forbidden[row, col]) d.Costs[row, col] = value;
        return DraftChanged.InvokeAsync(d);
    }
    private void SelectCell(int row, int col) { _row = row; _column = col; }
    private Task ToggleForbidden()
    {
        var d = Draft.Copy(); d.Forbidden[_row, _column] = !d.Forbidden[_row, _column];
        return DraftChanged.InvokeAsync(d);
    }
    private string CellClass(int row, int col, bool error) => row == col ? "diagonal" :
        $"{(row == _row || col == _column ? "active-axis" : "")} {(row == _row && col == _column ? "active-cell" : "")} {(Draft.Forbidden[row, col] ? "forbidden-cell" : "")} {(error ? "invalid-cell" : "")}";
    private async Task Move(KeyboardEventArgs e, int row, int col)
    {
        if (e.Key != "Enter") return;
        var next = (row + 1) % Draft.Size;
        if (next == col) next = (next + 1) % Draft.Size;
        await _inputs[next, col].FocusAsync();
    }
    private async Task ApplyPaste()
    {
        try { var draft = Draft.WithMatrixText(_pasteText); _pasteError = null; _row = _column = -1; await DraftChanged.InvokeAsync(draft); }
        catch (ArgumentException ex) { _pasteError = ex.Message; }
    }
}
