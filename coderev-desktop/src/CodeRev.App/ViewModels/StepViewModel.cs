using CodeRev.App.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeRev.App.ViewModels;

/// <summary>Lifecycle state of a pipeline step, driving its icon and colour.</summary>
public enum StepState { Running, Done, Failed, Cancelled }

/// <summary>Observable view of one pipeline step in the progress list.</summary>
public partial class StepViewModel : ObservableObject
{
    public int Id { get; init; }

    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string? _detail;
    [ObservableProperty] private StepState _state = StepState.Running;

    /// <summary>Icon-font glyph for the current state.</summary>
    public string Icon => State switch
    {
        StepState.Done => Icons.CircleCheck,
        StepState.Failed => Icons.AlertTriangle,
        StepState.Cancelled => Icons.CircleX,
        _ => Icons.Loader2,
    };

    // Booleans drive the theme-aware colour classes on the step icon (see MainWindow styles).
    public bool IsRunning => State == StepState.Running;
    public bool IsDone => State == StepState.Done;
    public bool IsFailed => State == StepState.Failed;
    public bool IsCancelled => State == StepState.Cancelled;

    partial void OnStateChanged(StepState value)
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsCancelled));
    }

    /// <summary>Marks a still-running step as cancelled (stops the spinner) when
    /// the user hits Stop.</summary>
    public void MarkCancelled()
    {
        if (State == StepState.Running)
            State = StepState.Cancelled;
    }

    public void MarkDone(long? durationMs)
    {
        State = StepState.Done;
        if (durationMs is { } ms)
        {
            var suffix = $"({ms} ms)";
            Detail = string.IsNullOrEmpty(Detail) ? suffix : $"{Detail}   {suffix}";
        }
    }

    public void MarkFailed(string? error)
    {
        State = StepState.Failed;
        Detail = error;
    }
}
