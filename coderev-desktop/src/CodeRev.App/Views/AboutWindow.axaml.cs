using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeRev.App.Localization;

namespace CodeRev.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // Show the real build version rather than a hard-coded one.
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        var version = v is null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}";
        SubtitleText.Text = Loc.Instance.T("AboutSubtitle", version);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
