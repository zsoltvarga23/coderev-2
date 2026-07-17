using System.Text.Json;

namespace CodeRev.Core.Settings;

/// <summary>Persisted UI preferences (currently just the theme).</summary>
public sealed record UiSettings
{
    /// <summary>"light", "dark" or "retro"; empty means follow the system.</summary>
    public string Theme { get; init; } = "";
}

/// <summary>
/// Tiny JSON-file store for UI preferences, saved in the per-user app-data
/// directory next to the review history. Load never throws — a missing,
/// corrupt or unreadable file yields defaults (best-effort, like the other
/// stores).
/// </summary>
public sealed class UiSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _file;

    public UiSettingsStore(string? filePath = null)
    {
        _file = filePath ?? DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
    }

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "coderev-desktop", "ui-settings.json");

    public UiSettings Load()
    {
        try
        {
            if (!File.Exists(_file))
                return new UiSettings();
            return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(_file), Options) ?? new UiSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UiSettings();
        }
    }

    public void Save(UiSettings settings)
    {
        try
        {
            File.WriteAllText(_file, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best-effort: never fail the app because a preference could not persist.
        }
    }
}
