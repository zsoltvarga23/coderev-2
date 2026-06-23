using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Threading;
using CodeRev.App.Localization;
using CodeRev.Core.Config;
using CodeRev.Core.Engine;
using CodeRev.Core.History;
using CodeRev.Core.Models;
using CodeRev.Core.Protocol;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeRev.App.ViewModels;

/// <summary>
/// Orchestrates a review run: launches the engine via <see cref="ICoderevRunner"/>
/// and folds the incoming NDJSON events into observable UI state (steps, diff,
/// streamed review). Engine events arrive off the UI thread, so each is
/// marshalled onto the dispatcher before touching observable collections.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICoderevRunner _runner;
    private readonly HistoryStore _history;
    private readonly Dictionary<int, StepViewModel> _stepById = new();
    private CancellationTokenSource? _cts;
    private string _lastDiffUnified = "";
    private int _changedCount;

    public MainWindowViewModel() : this(new CoderevRunner(), new HistoryStore()) { }

    public MainWindowViewModel(ICoderevRunner runner, HistoryStore? history = null)
    {
        _runner = runner;
        _history = history ?? new HistoryStore();
        RefreshHistory();
    }

    [ObservableProperty] private string _repositoryPath = "";
    [ObservableProperty] private string _branch = "";

    /// <summary>Local branches of the current repo, for branch autocomplete.</summary>
    public ObservableCollection<string> Branches { get; } = new();

    private CancellationTokenSource? _branchCts;

    // Refresh branch suggestions when the repo path changes (debounced, so
    // manual typing in the path field doesn't spawn git on every keystroke).
    partial void OnRepositoryPathChanged(string value)
    {
        _branchCts?.Cancel();
        _branchCts = new CancellationTokenSource();
        _ = RefreshBranchesAsync(value, _branchCts.Token);
    }

    private async Task RefreshBranchesAsync(string path, CancellationToken ct)
    {
        try { await Task.Delay(350, ct); }
        catch (OperationCanceledException) { return; }

        if (!Directory.Exists(path))
            return;

        var isRepo = await RepoInspector.IsGitRepositoryAsync(path, ct);
        var branches = isRepo
            ? await RepoInspector.ListLocalBranchesAsync(path, ct)
            : (IReadOnlyList<string>)Array.Empty<string>();
        if (ct.IsCancellationRequested)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Branches.Clear();
            foreach (var b in branches)
                Branches.Add(b);
            ImportRepoConfig();
            var note = CoderevConfig.Exists(path) ? Loc.Instance.T("StConfigImported") : "";
            StatusText = isRepo
                ? Loc.Instance.T("StBranchesLoaded", branches.Count) + note
                : Loc.Instance.T("StNotRepo");
        });
    }

    /// <summary>Loads base/agent/lang from the repo's .coderev.json into the
    /// form so the UI reflects the real config. If there is no config file the
    /// fields are cleared, leaving placeholders and letting the engine decide.</summary>
    public void ImportRepoConfig()
    {
        if (!string.IsNullOrWhiteSpace(RepositoryPath) && CoderevConfig.Exists(RepositoryPath))
        {
            var cfg = CoderevConfig.Load(RepositoryPath);
            BaseRef = cfg.BaseRef;
            Agent = cfg.Agent;
            Lang = cfg.Lang;
        }
        else
        {
            BaseRef = "";
            Agent = "";
            Lang = "";
        }
    }
    // Empty by default so the GUI does NOT override the repo's .coderev.json.
    // When a repo with a config is opened these are filled from it; otherwise
    // they stay blank (placeholder) and the engine resolves them itself.
    [ObservableProperty] private string _baseRef = "";
    [ObservableProperty] private string _agent = "";
    [ObservableProperty] private string _lang = "";
    [ObservableProperty] private bool _dryRun;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _statusText = Loc.Instance.T("StReady");
    [ObservableProperty] private string _metaText = "";
    [ObservableProperty] private string _reviewText = "";
    [ObservableProperty] private bool _hasSections;

    public ObservableCollection<StepViewModel> Steps { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    // D5: past runs.
    public ObservableCollection<ReviewHistoryEntry> History { get; } = new();

    [ObservableProperty] private ReviewHistoryEntry? _selectedHistoryEntry;

    partial void OnSelectedHistoryEntryChanged(ReviewHistoryEntry? value)
    {
        if (value is not null) LoadFromHistory(value);
    }

    // D3: structured diff and review for the rich views.
    public ObservableCollection<DiffFile> DiffFiles { get; } = new();
    public ObservableCollection<DiffLine> VisibleDiffLines { get; } = new();
    public ObservableCollection<ReviewSection> ReviewSections { get; } = new();

    /// <summary>Selected file in the diff sidebar; null shows all files.</summary>
    [ObservableProperty] private DiffFile? _selectedDiffFile;

    partial void OnSelectedDiffFileChanged(DiffFile? value) => RefreshVisibleDiff();

    /// <summary>Selectable agents and languages for the setup combo boxes.</summary>
    public string[] AgentOptions { get; } = { "codex", "copilot", "claude" };
    public string[] LangOptions { get; } = { "hu", "en" };

    /// <summary>App version (from the assembly), shown in the status bar.</summary>
    public string AppVersion { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "coderev Desktop" : $"coderev Desktop v{v.Major}.{v.Minor}.{v.Build}";
    }

    private bool CanRun => !IsRunning;
    private bool CanStop => IsRunning;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath) || string.IsNullOrWhiteSpace(Branch))
        {
            StatusText = Loc.Instance.T("StNeedRepoBranch");
            return;
        }

        Steps.Clear();
        Log.Clear();
        _stepById.Clear();
        DiffFiles.Clear();
        VisibleDiffLines.Clear();
        ReviewSections.Clear();
        HasSections = false;
        ReviewText = MetaText = "";
        IsRunning = true;
        StatusText = Loc.Instance.T("StRunning");
        _cts = new CancellationTokenSource();

        var options = new RunOptions
        {
            Branch = Branch,
            RepositoryPath = RepositoryPath,
            BaseRef = BaseRef,
            Agent = Agent,
            Lang = Lang,
            DryRun = DryRun,
        };

        try
        {
            await foreach (var ev in _runner.RunAsync(options, _cts.Token))
            {
                var captured = ev;
                await Dispatcher.UIThread.InvokeAsync(() => Handle(captured));
            }
            StatusText = Loc.Instance.T("StDone");
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc.Instance.T("StCancelled");
        }
        catch (Exception ex)
        {
            StatusText = Loc.Instance.T("StError", ex.Message);
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    /// <summary>Clears the diff file filter so all files are shown.</summary>
    [RelayCommand]
    private void ShowAllFiles() => SelectedDiffFile = null;

    private void Handle(CoderevEvent ev)
    {
        Log.Add(ev.Label is { } l ? $"{ev.Type}: {l}" : ev.Type);

        switch (ev.Type)
        {
            case EventType.RunStart:
                StatusText = Loc.Instance.T("StReviewOf", ev.Branch ?? "", ev.Base ?? "");
                break;

            case EventType.StepStart:
                var step = new StepViewModel { Id = ev.Id ?? Steps.Count + 1, Label = ev.Label ?? "" };
                _stepById[step.Id] = step;
                Steps.Add(step);
                break;

            case EventType.StepInfo:
                if (TryStep(ev.Id, out var si)) si.Detail = ev.Detail;
                break;

            case EventType.StepDone:
                if (TryStep(ev.Id, out var sd)) sd.MarkDone(ev.DurationMs);
                break;

            case EventType.StepFail:
                if (TryStep(ev.Id, out var sf)) sf.MarkFailed(ev.Error);
                break;

            case EventType.Warn:
                Log.Add("! " + ev.Message);
                break;

            case EventType.Meta:
                _changedCount = ev.ChangedFiles?.Count ?? 0;
                MetaText = Loc.Instance.T("MetaFormat", _changedCount, ev.Hunks ?? 0, ev.PromptBytes ?? 0);
                break;

            case EventType.Diff:
                _lastDiffUnified = ev.Unified ?? "";
                DiffFiles.Clear();
                foreach (var f in DiffParser.Parse(ev.Unified))
                    DiffFiles.Add(f);
                SelectedDiffFile = null; // show all files
                RefreshVisibleDiff();
                break;

            case EventType.Stream:
                ReviewText += ev.Chunk;
                break;

            case EventType.Review:
                if (ev.Markdown is not null)
                {
                    ReviewText = ev.Markdown;
                    ReviewSections.Clear();
                    foreach (var s in ReviewParser.Parse(ev.Markdown))
                        ReviewSections.Add(s);
                    HasSections = ReviewSections.Count > 0;
                }
                break;

            case EventType.Summary:
                StatusText = ev.OutPath is { Length: > 0 }
                    ? Loc.Instance.T("StSummaryOut", ev.OutPath, ev.TotalMs ?? 0)
                    : Loc.Instance.T("StSummary", ev.TotalMs ?? 0);
                break;

            case EventType.Done:
                StatusText += ev.ExitCode == 0 ? "  ✓" : $"  ✗ ({ev.ExitCode})";
                SaveToHistory();
                break;
        }
    }

    /// <summary>Builds a history entry from the current review state (used for
    /// both saving to history and exporting).</summary>
    public ReviewHistoryEntry BuildCurrentEntry() =>
        ReviewHistoryEntry.Create(Branch, BaseRef, Agent, Lang, _changedCount, ReviewText, _lastDiffUnified);

    private void SaveToHistory()
    {
        try
        {
            _history.Save(BuildCurrentEntry());
            RefreshHistory();
        }
        catch
        {
            // History is best-effort; never fail a run because of it.
        }
    }

    private void RefreshHistory()
    {
        History.Clear();
        foreach (var e in _history.List())
            History.Add(e);
    }

    private void LoadFromHistory(ReviewHistoryEntry e)
    {
        ReviewText = e.ReviewMarkdown;
        ReviewSections.Clear();
        foreach (var s in ReviewParser.Parse(e.ReviewMarkdown))
            ReviewSections.Add(s);
        HasSections = ReviewSections.Count > 0;

        _lastDiffUnified = e.DiffUnified;
        DiffFiles.Clear();
        foreach (var f in DiffParser.Parse(e.DiffUnified))
            DiffFiles.Add(f);
        SelectedDiffFile = null;
        RefreshVisibleDiff();

        StatusText = Loc.Instance.T("StHistoryLoaded", e.Label);
    }

    /// <summary>Rebuilds the visible diff lines for the selected file (or all
    /// files), injecting a header line before each file.</summary>
    private void RefreshVisibleDiff()
    {
        VisibleDiffLines.Clear();
        var files = SelectedDiffFile is null
            ? (IEnumerable<DiffFile>)DiffFiles
            : new[] { SelectedDiffFile };
        foreach (var f in files)
        {
            VisibleDiffLines.Add(new DiffLine(DiffLineKind.Meta, $"▸ {f.Summary}", null, null));
            foreach (var line in f.Lines)
                VisibleDiffLines.Add(line);
        }
    }

    private bool TryStep(int? id, out StepViewModel step)
    {
        if (id is { } v && _stepById.TryGetValue(v, out var found))
        {
            step = found;
            return true;
        }
        // Fall back to the most recent step if the event omitted an id.
        if (Steps.Count > 0)
        {
            step = Steps[^1];
            return true;
        }
        step = null!;
        return false;
    }
}
