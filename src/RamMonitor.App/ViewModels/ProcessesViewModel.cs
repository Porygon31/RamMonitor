using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RamMonitor.App.Composition;
using RamMonitor.Core.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Core.Services;

namespace RamMonitor.App.ViewModels;

/// <summary>
/// Affiche la liste des process avec recherche, sélection, et actions :
/// kill, empty working set, set priority, dump mémoire.
/// </summary>
public sealed partial class ProcessesViewModel : ViewModelBase, IDisposable
{
    private readonly MonitoringEngine _engine;
    private readonly IProcessActionService _actionService;
    private readonly IMemoryDumpService _dumpService;
    private readonly IElevationService _elevationService;
    private readonly AppOptions _options;
    private readonly Dictionary<int, ProcessRowViewModel> _rowsByPid = new();

    public ObservableCollection<ProcessRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ProcessRowViewModel? _selected;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isElevated;

    public IReadOnlyList<ProcessPriorityLevel> PriorityLevels { get; } =
        Enum.GetValues<ProcessPriorityLevel>();

    public ProcessesViewModel(MonitoringEngine engine, IProcessActionService actionService,
        IMemoryDumpService dumpService, IElevationService elevationService, AppOptions options)
    {
        _engine = engine;
        _actionService = actionService;
        _dumpService = dumpService;
        _elevationService = elevationService;
        _options = options;
        _isElevated = elevationService.IsCurrentProcessElevated;

        _engine.FastTick += OnFastTick;
    }

    private void OnFastTick(FastTickResult result)
    {
        var top = result.TopProcesses
            .OrderByDescending(p => p.WorkingSetBytes)
            .Take(_options.Ui.MaxProcessRows)
            .ToList();

        var seen = new HashSet<int>(top.Count);
        var filter = SearchText?.Trim() ?? string.Empty;

        RunOnUi(() =>
        {
            foreach (var snap in top)
            {
                seen.Add(snap.ProcessId);

                if (_rowsByPid.TryGetValue(snap.ProcessId, out var row))
                {
                    row.Update(snap);
                }
                else
                {
                    row = new ProcessRowViewModel(snap);
                    _rowsByPid[snap.ProcessId] = row;
                    if (MatchesFilter(row, filter))
                    {
                        Rows.Add(row);
                    }
                }
            }

            // Retirer les process disparus.
            var disappeared = _rowsByPid.Keys.Where(pid => !seen.Contains(pid)).ToArray();
            foreach (var pid in disappeared)
            {
                var row = _rowsByPid[pid];
                _rowsByPid.Remove(pid);
                Rows.Remove(row);
            }

            // Réappliquer le filtre.
            if (!string.IsNullOrEmpty(filter))
            {
                foreach (var row in _rowsByPid.Values)
                {
                    var inList = Rows.Contains(row);
                    var matches = MatchesFilter(row, filter);
                    if (matches && !inList) Rows.Add(row);
                    else if (!matches && inList) Rows.Remove(row);
                }
            }
        });
    }

    private static bool MatchesFilter(ProcessRowViewModel row, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        return row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || row.ProcessId.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchTextChanged(string value)
    {
        RunOnUi(() =>
        {
            Rows.Clear();
            foreach (var row in _rowsByPid.Values.OrderByDescending(r => r.WorkingSetBytes))
            {
                if (MatchesFilter(row, value ?? string.Empty))
                {
                    Rows.Add(row);
                }
            }
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task KillSelectedAsync()
    {
        if (Selected is null) return;

        var result = MessageBox.Show(
            $"Terminer le process {Selected.Name} (PID {Selected.ProcessId}) ?",
            "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var ok = await _actionService.KillAsync(Selected.ProcessId).ConfigureAwait(true);
        StatusMessage = ok
            ? $"Process {Selected.ProcessId} terminé."
            : $"Échec — élévation nécessaire pour {Selected.Name} ?";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EmptyWorkingSetSelectedAsync()
    {
        if (Selected is null) return;
        var ok = await _actionService.EmptyWorkingSetAsync(Selected.ProcessId).ConfigureAwait(true);
        StatusMessage = ok ? "Working set vidé." : "Échec (privilèges insuffisants).";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SetPriorityAsync(ProcessPriorityLevel priority)
    {
        if (Selected is null) return;
        var ok = await _actionService.SetPriorityAsync(Selected.ProcessId, priority).ConfigureAwait(true);
        StatusMessage = ok ? $"Priorité réglée sur {priority}." : "Échec (privilèges insuffisants).";
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndElevated))]
    private async Task CreateMiniDumpAsync()
    {
        if (Selected is null) return;
        var folder = Path.Combine(AppContext.BaseDirectory, _options.Export.DefaultFolder);
        var path = await _dumpService.CreateDumpAsync(Selected.ProcessId, DumpKind.MiniDump, folder)
            .ConfigureAwait(true);
        StatusMessage = path is null
            ? "Échec du dump (vérifier l'élévation et SeDebug)."
            : $"Dump créé : {path}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectionAndElevated))]
    private async Task CreateFullDumpAsync()
    {
        if (Selected is null) return;
        var folder = Path.Combine(AppContext.BaseDirectory, _options.Export.DefaultFolder);
        var path = await _dumpService.CreateDumpAsync(Selected.ProcessId, DumpKind.FullDump, folder)
            .ConfigureAwait(true);
        StatusMessage = path is null
            ? "Échec du dump."
            : $"Full dump créé : {path}";
    }

    private bool HasSelection() => Selected is not null;
    private bool HasSelectionAndElevated() => Selected is not null && IsElevated;

    partial void OnSelectedChanged(ProcessRowViewModel? value)
    {
        KillSelectedCommand.NotifyCanExecuteChanged();
        EmptyWorkingSetSelectedCommand.NotifyCanExecuteChanged();
        SetPriorityCommand.NotifyCanExecuteChanged();
        CreateMiniDumpCommand.NotifyCanExecuteChanged();
        CreateFullDumpCommand.NotifyCanExecuteChanged();
    }

    public void Dispose() => _engine.FastTick -= OnFastTick;
}
