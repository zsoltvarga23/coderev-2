using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CodeRev.App.Localization;
using CodeRev.App.ViewModels;
using CodeRev.Core.Export;

namespace CodeRev.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>Switches the UI language (English &lt;-&gt; Hungarian) live.</summary>
    private void OnToggleLanguage(object? sender, RoutedEventArgs e) => Loc.Instance.Toggle();

    /// <summary>Opens the About window (app description and AI usage).</summary>
    private async void OnOpenAbout(object? sender, RoutedEventArgs e)
    {
        var about = new AboutWindow();
        await about.ShowDialog(this);
    }

    /// <summary>Opens the .coderev.json editor for the current repository path.</summary>
    private async void OnOpenConfigEditor(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        // Pass the actual repo path (possibly empty). The editor disables Save
        // unless a real folder is open, so the config never lands in a random spot.
        var editor = new ConfigEditorWindow
        {
            DataContext = new ConfigEditorViewModel(vm.RepositoryPath),
        };
        await editor.ShowDialog(this);

        // The user may have saved changes; reflect them in the main form.
        vm.ImportRepoConfig();
    }

    /// <summary>Opens a folder picker and sets it as the repository path
    /// (which triggers branch autocomplete to refresh).</summary>
    private async void OnBrowseRepo(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Repository / mappa kiválasztása",
            AllowMultiple = false,
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            vm.RepositoryPath = path;
    }

    private async void OnExportMarkdown(object? sender, RoutedEventArgs e) => await ExportAsync(html: false);
    private async void OnExportHtml(object? sender, RoutedEventArgs e) => await ExportAsync(html: true);

    /// <summary>Exports the current review to a file the user picks.</summary>
    private async Task ExportAsync(bool html)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var entry = vm.BuildCurrentEntry();
        if (string.IsNullOrWhiteSpace(entry.ReviewMarkdown) && string.IsNullOrWhiteSpace(entry.DiffUnified))
        {
            vm.StatusText = Loc.Instance.T("StNothingExport");
            return;
        }

        var ext = html ? "html" : "md";
        var content = html ? ReviewExporter.ToHtml(entry) : ReviewExporter.ToMarkdown(entry);
        var safeBranch = entry.Branch.Replace('/', '-');

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Review exportálása",
            SuggestedFileName = $"review-{safeBranch}.{ext}",
            DefaultExtension = ext,
        });
        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        vm.StatusText = Loc.Instance.T("StExported", file.Name);
    }

    /// <summary>Toggles the application between light and dark themes.</summary>
    private void OnToggleTheme(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant =
                app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }
}
