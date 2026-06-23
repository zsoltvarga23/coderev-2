using CommunityToolkit.Mvvm.ComponentModel;

namespace CodeRev.App.ViewModels;

/// <summary>Observable view of one pipeline step in the progress list.</summary>
public partial class StepViewModel : ObservableObject
{
    public int Id { get; init; }

    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string? _detail;
    [ObservableProperty] private string _glyph = "…";

    public void MarkDone(long? durationMs)
    {
        Glyph = "✓";
        if (durationMs is { } ms)
        {
            var suffix = $"({ms} ms)";
            Detail = string.IsNullOrEmpty(Detail) ? suffix : $"{Detail}   {suffix}";
        }
    }

    public void MarkFailed(string? error)
    {
        Glyph = "✗";
        Detail = error;
    }
}
