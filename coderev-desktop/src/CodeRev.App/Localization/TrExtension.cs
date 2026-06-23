using Avalonia.Data;

namespace CodeRev.App.Localization;

/// <summary>
/// XAML markup extension: <c>{loc:Tr SomeKey}</c> binds a property to the
/// localized string for <c>SomeKey</c>, refreshing automatically when the UI
/// language changes (see <see cref="Loc"/>).
/// </summary>
public sealed class TrExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public IBinding ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
