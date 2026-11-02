using Microsoft.AspNetCore.Components;
using RouteOptimizer.Application;
using RouteOptimizer.Components;
using RouteOptimizer.Domain;
using RouteOptimizer.Models;

namespace RouteOptimizer.Pages;

public partial class Home
{
    [Inject]
    public ILittleSolver Solver { get; set; } = default!;
    private ProblemDraft _draft = ProblemDraft.From(ProblemSamples.TeachingExample());
    private TspProblem? _problem;
    private ISearchSession? _session;
    private MatrixEditor? _editor;
    private CancellationTokenSource? _runCancellation;
    private bool _busy,
        _disposed;
    private int _selected,
        _tab;
    private string? _error,
        _notice;
    private string _datasetName = "Пример: обслуживание инфраструктуры";
    private SearchSnapshot? SelectedStep =>
        _session is { Steps.Count: > 0 }
            ? _session.Steps[Math.Min(_selected, _session.Steps.Count - 1)]
            : null;

    private void OnDraftChanged(ProblemDraft draft)
    {
        if (_session is not null)
            _notice = "Исходные данные изменены. Предыдущий расчёт сброшен.";
        _draft = draft;
        _session = null;
        _problem = null;
        _selected = 0;
        _error = null;
        _datasetName = "Пользовательская матрица";
    }

    private void LoadExample(string example)
    {
        var problem =
            example == "teaching"
                ? ProblemSamples.TeachingExample()
                : ProblemSamples.Experiment(int.Parse(example));
        _draft = ProblemDraft.From(problem);
        ClearSession();
        _datasetName =
            example == "teaching"
                ? "Пример: обслуживание инфраструктуры"
                : $"Эксперимент: {example} объектов · зерно 2026";
        _notice = "Пример загружен. Можно сразу запустить расчёт или изменить стоимости.";
    }

    private void ClearSession()
    {
        _session = null;
        _problem = null;
        _selected = 0;
        _error = null;
        _notice = null;
    }

    private void Reset()
    {
        ClearSession();
        _notice = "Расчёт сброшен. Исходная матрица сохранена.";
        _tab = 0;
    }

    private void SelectStep(int index) => _selected = index;

    private void SelectNode(int node)
    {
        if (_session is null)
            return;
        for (var i = 0; i < _session.Steps.Count; i++)
            if (_session.Steps[i].NodeId == node)
            {
                _selected = i;
                return;
            }
    }

    private void SelectSolutionStep()
    {
        if (_session is null)
            return;
        for (var i = _session.Steps.Count - 1; i >= 0; i--)
            if (
                _session.Steps[i].State == NodeState.Complete
                || _session.Steps[i].Children.Any(c => c.State == NodeState.Complete)
            )
            {
                _selected = i;
                return;
            }
    }

    private async Task<bool> EnsureSessionAsync()
    {
        if (_session is not null)
            return true;
        try
        {
            _problem = _draft.Build();
            _session = Solver.CreateSession(_problem);
            _error = _notice = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            _error = ex.Message;
            _tab = 0;
            StateHasChanged();
            await Task.Delay(1);
            if (_editor is not null)
                await _editor.FocusFirstErrorAsync();
            return false;
        }
    }

    private async Task AdvanceAsync()
    {
        if (_busy || !await EnsureSessionAsync())
            return;
        _session!.Advance();
        _selected = Math.Max(0, _session.Steps.Count - 1);
        _tab = 1;
    }

    private async Task RunAsync()
    {
        if (_busy)
            return;
        if (
            _session?.Result.Status
            is SearchStatus.Optimal
                or SearchStatus.Infeasible
                or SearchStatus.Stopped
        )
            ClearSession();
        if (!await EnsureSessionAsync())
            return;
        if (_tab != 1)
            _tab = 2;
        _busy = true;
        using var cancellation = new CancellationTokenSource();
        _runCancellation = cancellation;
        try
        {
            while (
                _session!.Result.Status == SearchStatus.Running
                && !cancellation.IsCancellationRequested
            )
            {
                for (var i = 0; i < 10 && _session.Result.Status == SearchStatus.Running; i++)
                    _session.Advance(cancellation.Token);
                _selected = Math.Max(0, _session.Steps.Count - 1);
                StateHasChanged();
                await Task.Delay(16, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            _busy = false;
            _runCancellation = null;
        }
    }

    private void Stop()
    {
        _session?.Stop();
        _runCancellation?.Cancel();
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            Stop();
        }
        return ValueTask.CompletedTask;
    }
}
