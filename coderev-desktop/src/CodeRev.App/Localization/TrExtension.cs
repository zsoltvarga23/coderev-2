using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace CodeRev.App.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Tr SomeKey}</c> binds a property to the
/// localized string for <c>SomeKey</c>. It binds to <see cref="Loc.Language"/>
/// (a normal notifying property) and resolves the key via a converter, so every
/// label refreshes live when the language changes — Avalonia reliably refreshes
/// bindings to named properties (unlike indexer-path bindings).
/// </summary>
public sealed class TrExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public IBinding ProvideValue(IServiceProvider serviceProvider) =>
        new Binding(nameof(Loc.Language))
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay,
            Converter = LocLookupConverter.Instance,
            ConverterParameter = Key,
        };
}

/// <summary>Resolves a localization key (the converter parameter) to its current
/// localized string; the bound <see cref="Loc.Language"/> value is only the
/// change trigger.</summary>
public sealed class LocLookupConverter : IValueConverter
{
    public static readonly LocLookupConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Loc.Instance[parameter as string ?? ""];

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
