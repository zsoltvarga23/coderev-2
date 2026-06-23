using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CodeRev.Core.Models;

namespace CodeRev.App.Converters;

/// <summary>Maps a diff line kind to a row background. Semi-transparent colors
/// read well on both light and dark themes.</summary>
public sealed class DiffBackgroundConverter : IValueConverter
{
    private static readonly IBrush Added = new SolidColorBrush(Color.FromArgb(48, 46, 160, 67));
    private static readonly IBrush Removed = new SolidColorBrush(Color.FromArgb(48, 203, 36, 49));
    private static readonly IBrush Hunk = new SolidColorBrush(Color.FromArgb(32, 3, 102, 214));
    private static readonly IBrush Meta = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            DiffLineKind.Added => Added,
            DiffLineKind.Removed => Removed,
            DiffLineKind.Hunk => Hunk,
            DiffLineKind.Meta => Meta,
            _ => Brushes.Transparent,
        };

    public object? ConvertBack(object? value, Type t, object? p, CultureInfo c) => null;
}

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
