using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CodeRev.App.Localization;
using CodeRev.App.Services;
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

    /// <summary>Checks GitHub Releases for a newer version and, on confirmation,
    /// downloads it and restarts into the new build (via Velopack). No-op with a
    /// note when running an un-installed dev/portable build.</summary>
    private async void OnCheckForUpdates(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var svc = new UpdateService();
        if (!svc.IsInstalled)
        {
            vm.StatusText = Loc.Instance.T("StUpdDevBuild");
            return;
        }

        vm.StatusText = Loc.Instance.T("StUpdChecking");
        try
        {
            var update = await svc.CheckForUpdatesAsync();
            var current = svc.CurrentVersion ?? "?";
            if (update is null)
            {
                vm.StatusText = Loc.Instance.T("StUpdNone", current);
                return;
            }

            var newVersion = update.TargetFullRelease.Version.ToString();
            var confirmed = await ConfirmAsync(
                Loc.Instance.T("UpdDlgTitle"),
                Loc.Instance.T("UpdDlgBody", newVersion, current));
            if (!confirmed)
                return;

            // Progress arrives off the UI thread; marshal back before touching VM.
            await svc.DownloadAsync(update, p =>
                Dispatcher.UIThread.Post(() => vm.StatusText = Loc.Instance.T("StUpdDownloading", p)));

            vm.StatusText = Loc.Instance.T("StUpdRestarting");
            svc.ApplyAndRestart(update); // exits this process and relaunches
        }
        catch (Exception ex)
        {
            vm.StatusText = Loc.Instance.T("StUpdError", ex.Message);
        }
    }

    /// <summary>Minimal modal yes/no confirmation, built in code to avoid a
    /// dedicated XAML window. Returns false if the dialog is closed any other way.</summary>
    private async Task<bool> ConfirmAsync(string title, string body)
    {
        var tcs = new TaskCompletionSource<bool>();

        var yes = new Button { Content = Loc.Instance.T("UpdYes"), IsDefault = true };
        var no = new Button { Content = Loc.Instance.T("UpdNo"), IsCancel = true };

        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { no, yes },
                    },
                },
            },
        };

        yes.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        no.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }
}
