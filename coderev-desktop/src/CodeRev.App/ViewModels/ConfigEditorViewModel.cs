using CodeRev.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeRev.App.ViewModels;

/// <summary>
/// Edits a repository's <c>.coderev.json</c> (the GUI equivalent of the CLI
/// <c>init</c>). Loads the existing file or defaults, validates on save, and
/// writes back in the engine-compatible kebab-case format. The agent manager is
/// the built-in agent picker plus an advanced custom-agent JSON field.
/// </summary>
public partial class ConfigEditorViewModel : ObservableObject
{
    private readonly string _repoPath;
    private CoderevConfig _config = new();

    public ConfigEditorViewModel(string repoPath)
    {
        _repoPath = repoPath;
        Load();
    }

    public string RepoPath => _repoPath;

    /// <summary>Saving is only allowed into a real, open folder/repository, so we
    /// never write .coderev.json to an arbitrary location.</summary>
    public bool CanSave => !string.IsNullOrWhiteSpace(_repoPath) && Directory.Exists(_repoPath);

    public string[] AgentOptions { get; } = { "codex", "copilot", "claude" };
    public string[] LangOptions { get; } = { "hu", "en" };
    public string[] ModeOptions { get; } = { "stdin", "arg", "file" };

    [ObservableProperty] private string _baseRef = "";
    [ObservableProperty] private string _headRef = "";
    [ObservableProperty] private string _agent = "codex";
    [ObservableProperty] private string _lang = "hu";
    [ObservableProperty] private string _template = "";
    [ObservableProperty] private string _out = "";
    [ObservableProperty] private bool _includeFullFiles;
    [ObservableProperty] private bool _strictFetch;
    [ObservableProperty] private bool _noProgress;
    [ObservableProperty] private int _contextLines;
    [ObservableProperty] private int _agentTimeout;
    [ObservableProperty] private string _obeyDocText = "";   // newline-separated
    [ObservableProperty] private string _agentConfigText = ""; // raw JSON (advanced)
    [ObservableProperty] private string _statusText = "";

    /// <summary>True while the custom-agent JSON should be shown/used.</summary>
    [ObservableProperty] private bool _useCustomAgent;

    private void Load()
    {
        _config = CoderevConfig.Load(_repoPath);
        BaseRef = _config.BaseRef;
        HeadRef = _config.HeadRef;
        Agent = _config.Agent;
        Lang = _config.Lang;
        Template = _config.Template;
        Out = _config.Out;
        IncludeFullFiles = _config.IncludeFullFiles;
        StrictFetch = _config.StrictFetch;
        NoProgress = _config.NoProgress;
        ContextLines = _config.ContextLines;
        AgentTimeout = _config.AgentTimeout;
        ObeyDocText = string.Join("\n", _config.ObeyDoc);
        AgentConfigText = _config.AgentConfig ?? "";
        UseCustomAgent = !string.IsNullOrWhiteSpace(_config.AgentConfig);

        if (!CanSave)
            StatusText = "Nincs megnyitott mappa/repository — a mentés nem elérhető. " +
                         "Nyiss meg egyet a főablakban (📂).";
        else if (CoderevConfig.Exists(_repoPath))
            StatusText = $"Betöltve: {CoderevConfig.PathFor(_repoPath)}";
        else
            StatusText = "Nincs még .coderev.json — alapértelmezett értékek.";
    }

    [RelayCommand]
    private void Reload() => Load();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Lang is not ("hu" or "en"))
        {
            StatusText = "A nyelv csak hu vagy en lehet.";
            return;
        }
        if (UseCustomAgent && !string.IsNullOrWhiteSpace(AgentConfigText) && !IsValidJson(AgentConfigText))
        {
            StatusText = "Az egyedi agent nem érvényes JSON.";
            return;
        }

        _config.BaseRef = BaseRef.Trim();
        _config.HeadRef = HeadRef.Trim();
        _config.Agent = Agent;
        _config.Lang = Lang;
        _config.Template = Template.Trim();
        _config.Out = Out.Trim();
        _config.IncludeFullFiles = IncludeFullFiles;
        _config.StrictFetch = StrictFetch;
        _config.NoProgress = NoProgress;
        _config.ContextLines = ContextLines;
        _config.AgentTimeout = AgentTimeout;
        _config.ObeyDoc = ObeyDocText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        _config.AgentConfig = UseCustomAgent && !string.IsNullOrWhiteSpace(AgentConfigText)
            ? AgentConfigText.Trim()
            : null;

        try
        {
            var path = _config.Save(_repoPath);
            StatusText = $"Mentve: {path}";
        }
        catch (Exception ex)
        {
            StatusText = "Hiba a mentéskor: " + ex.Message;
        }
    }

    private static bool IsValidJson(string s)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(s);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
