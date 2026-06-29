using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
using CodeRev.Core.History;

namespace CodeRev.App.Views;

public partial class MainWindow : Window
{
    // Set by the startup update check; the update button is shown (and acts) only
    // when a newer version was actually found.
    private UpdateService? _updateService;
    private Velopack.UpdateInfo? _pendingUpdate;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;

        // Open the suggestion list on click. Handled in the tunnel phase so it
        // fires on the box before its inner TextBox swallows the pointer, and on
        // the box (not the popup) so selecting an item doesn't reopen the list.
        foreach (var box in new[] { RepoBox, BranchBox, BaseBox })
            box.AddHandler(PointerPressedEvent, OnSuggestBoxPointerPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>On launch, quietly check GitHub Releases. If a newer version is
    /// available (and this is a real installed build), reveal the update button;
    /// otherwise it stays hidden. Failures (offline, no release) are ignored.</summary>
    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened; // run once
        if (DataContext is not MainWindowViewModel vm)
            return;
        try
        {
            _updateService = new UpdateService();
            if (!_updateService.IsInstalled)
                return; // dev/portable build: no in-app updates, keep the button hidden
            var update = await _updateService.CheckForUpdatesAsync();
            if (update is null)
                return; // up to date
            _pendingUpdate = update;
            vm.UpdateTipText = Loc.Instance.T("UpdAvailableTip", update.TargetFullRelease.Version.ToString());
            vm.UpdateAvailable = true;
        }
        catch
        {
            // Network error / no published release — leave the button hidden.
        }
    }

    /// <summary>Removes a repository from the recent-repos dropdown (× button).
    /// Handled here (not via a binding) so the click doesn't also select the item
    /// in the AutoCompleteBox.</summary>
    private void OnRemoveRecent(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is Button { DataContext: RecentRepository repo })
            vm.RemoveRecent(repo);
        e.Handled = true;
    }

    /// <summary>Opens an autocomplete box's suggestion list when the field is
    /// clicked, so the recent repos / branches / base refs are offered on click,
    /// not only on typing. Deferred so it runs after the control's own pointer
    /// handling; keying off a click on the box (not focus) avoids reopening the
    /// list right after an item is selected. Clicking while it is already open is
    /// a no-op (close with Escape or by clicking away) — matching the
    /// always-show, MinimumPrefixLength=0 design.</summary>
    private void OnSuggestBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is AutoCompleteBox box)
            Dispatcher.UIThread.Post(() => box.IsDropDownOpen = true);
    }

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

    /// <summary>Installs the update found at startup: confirm, download (with
    /// progress) and restart into the new build via Velopack. The button this is
    /// wired to is only visible when <see cref="_pendingUpdate"/> is set.</summary>
    private async void OnUpdate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || _updateService is null || _pendingUpdate is null)
            return;

        var update = _pendingUpdate;
        var current = _updateService.CurrentVersion ?? "?";
        var newVersion = update.TargetFullRelease.Version.ToString();
        try
        {
            var confirmed = await ConfirmAsync(
                Loc.Instance.T("UpdDlgTitle"),
                Loc.Instance.T("UpdDlgBody", newVersion, current));
            if (!confirmed)
                return;

            // Cancel the download if the window is closed mid-flight, so the
            // progress callback never fires against a torn-down VM.
            using var cts = new CancellationTokenSource();
            void cancelOnClose(object? s, EventArgs ev) => cts.Cancel();
            Closed += cancelOnClose;
            try
            {
                // Progress arrives off the UI thread; marshal back before touching VM.
                await _updateService.DownloadAsync(update, p =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (!cts.IsCancellationRequested)
                            vm.StatusText = Loc.Instance.T("StUpdDownloading", p);
                    }), cts.Token);
            }
            finally
            {
                Closed -= cancelOnClose;
            }

            vm.StatusText = Loc.Instance.T("StUpdRestarting");
            _updateService.ApplyAndRestart(update); // exits this process and relaunches
        }
        catch (OperationCanceledException)
        {
            // Window closed during download — nothing to report.
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
            ShowInTaskbar = false,
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
