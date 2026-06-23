using System.Text;
using CodeRev.Core.Protocol;

namespace CodeRev.Core.Models;

public enum StepStatus { Running, Done, Failed }

/// <summary>State of one pipeline step (repo check, diff, agent, ...).</summary>
public sealed class StepState
{
    public int Id { get; init; }
    public string Label { get; set; } = "";
    public string? Detail { get; set; }
    public StepStatus Status { get; set; } = StepStatus.Running;
    public long? DurationMs { get; set; }
    public string? Error { get; set; }
}

public sealed record MetaState(
    IReadOnlyList<string> ChangedFiles,
    int Hunks,
    int DiffBytes,
    int PromptBytes);

/// <summary>
/// Folds the engine event stream into structured, observable-friendly state.
/// The GUI feeds events in via <see cref="Apply"/> and renders the resulting
/// steps, diff, and review. Keeping the fold here (UI-free) makes it testable
/// and shareable across views.
/// </summary>
public sealed class ReviewSession
{
    private readonly List<StepState> _steps = new();
    private readonly List<string> _warnings = new();
    private readonly StringBuilder _streamed = new();

    public string? Version { get; private set; }
    public string? Branch { get; private set; }
    public string? Base { get; private set; }
    public string? Lang { get; private set; }

    public IReadOnlyList<StepState> Steps => _steps;
    public IReadOnlyList<string> Warnings => _warnings;
    public MetaState? Meta { get; private set; }
    public string? Diff { get; private set; }

    /// <summary>The review text: the final <c>review</c> event if seen, else the
    /// concatenation of streamed chunks received so far.</summary>
    public string ReviewMarkdown { get; private set; } = "";

    public long? TotalMs { get; private set; }
    public string? OutPath { get; private set; }
    public int? ExitCode { get; private set; }
    public bool IsComplete { get; private set; }

    /// <summary>Raised after each applied event so the UI can refresh.</summary>
    public event Action<CoderevEvent>? Applied;

    public void Apply(CoderevEvent ev)
    {
        switch (ev.Type)
        {
            case EventType.RunStart:
                Version = ev.Version; Branch = ev.Branch; Base = ev.Base; Lang = ev.Lang;
                break;

            case EventType.StepStart:
                _steps.Add(new StepState { Id = ev.Id ?? _steps.Count + 1, Label = ev.Label ?? "" });
                break;

            case EventType.StepInfo:
                if (Find(ev.Id) is { } si) si.Detail = ev.Detail;
                break;

            case EventType.StepDone:
                if (Find(ev.Id) is { } sd)
                {
                    sd.Status = StepStatus.Done;
                    sd.DurationMs = ev.DurationMs;
                }
                break;

            case EventType.StepFail:
                if (Find(ev.Id) is { } sf)
                {
                    sf.Status = StepStatus.Failed;
                    sf.Error = ev.Error;
                }
                break;

            case EventType.Warn:
                if (!string.IsNullOrEmpty(ev.Message)) _warnings.Add(ev.Message!);
                break;

            case EventType.Meta:
                Meta = new MetaState(ev.ChangedFiles ?? Array.Empty<string>(),
                    ev.Hunks ?? 0, ev.DiffBytes ?? 0, ev.PromptBytes ?? 0);
                break;

            case EventType.Diff:
                Diff = ev.Unified;
                break;

            case EventType.Stream:
                if (ev.Chunk is { Length: > 0 })
                {
                    _streamed.Append(ev.Chunk);
                    ReviewMarkdown = _streamed.ToString();
                }
                break;

            case EventType.Review:
                if (ev.Markdown is not null) ReviewMarkdown = ev.Markdown;
                break;

            case EventType.Summary:
                TotalMs = ev.TotalMs; OutPath = ev.OutPath;
                break;

            case EventType.Done:
                ExitCode = ev.ExitCode; IsComplete = true;
                break;
        }
        Applied?.Invoke(ev);
    }

    /// <summary>Finds a step by id, falling back to the most recent step when the
    /// event omits an id (defensive against protocol drift).</summary>
    private StepState? Find(int? id)
    {
        if (id is { } v)
        {
            for (var i = _steps.Count - 1; i >= 0; i--)
                if (_steps[i].Id == v) return _steps[i];
        }
        return _steps.Count > 0 ? _steps[^1] : null;
    }
}
