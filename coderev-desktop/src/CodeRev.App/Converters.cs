using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CodeRev.Core.Models;

namespace CodeRev.App.Converters;

/// <summary>Formats a diff line's gutter number (new side, else old side).</summary>
public sealed class DiffLineNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffLine line ? (line.NewNo ?? line.OldNo)?.ToString() ?? "" : "";

    public object? ConvertBack(object? value, Type t, object? p, CultureInfo c) => null;
}

/// <summary>Maps a review section severity to a header background color.</summary>
public sealed class SeverityBrushConverter : IValueConverter
{
    private static readonly IBrush Summary = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush Major = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush Minor = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush Tests = new SolidColorBrush(Color.Parse("#8B5CF6"));
    private static readonly IBrush Suggestions = new SolidColorBrush(Color.Parse("#10B981"));
    private static readonly IBrush Other = new SolidColorBrush(Color.Parse("#6B7280"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ReviewSeverity.Summary => Summary,
            ReviewSeverity.Major => Major,
            ReviewSeverity.Minor => Minor,
            ReviewSeverity.Tests => Tests,
            ReviewSeverity.Suggestions => Suggestions,
            _ => Other,
        };

    public object? ConvertBack(object? value, Type t, object? p, CultureInfo c) => null;
}
