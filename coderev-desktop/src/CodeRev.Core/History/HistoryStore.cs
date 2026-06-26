using System.Text.Json;

namespace CodeRev.Core.History;

/// <summary>A persisted record of one completed review run.</summary>
public sealed record ReviewHistoryEntry
{
    public string Id { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
    public string Branch { get; init; } = "";
    public string Base { get; init; } = "";
    public string Agent { get; init; } = "";
    public string Lang { get; init; } = "";
    public int ChangedFiles { get; init; }
    public string ReviewMarkdown { get; init; } = "";
    public string DiffUnified { get; init; } = "";

    /// <summary>Repository this review ran against — the repo name, or its local
    /// root folder name when no name is available. Used as a history chip/filter.
    /// Empty for entries saved before this field existed.</summary>
    public string Repository { get; init; } = "";

    /// <summary>Short label for history lists, e.g. "2026-06-23 15:30 — feature/login".</summary>
    public string Label => $"{Timestamp.LocalDateTime:yyyy-MM-dd HH:mm} — {Branch}";

    /// <summary>Derives a repository display name from a path: the last path
    /// segment (the folder name), or "" if the path is empty.</summary>
    public static string RepositoryNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        var trimmed = path.TrimEnd('/', '\\', ' ');
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    /// <summary>Creates an entry with a sortable, filesystem-safe id.</summary>
    public static ReviewHistoryEntry Create(string branch, string @base, string agent,
        string lang, int changedFiles, string reviewMarkdown, string diffUnified,
        string repository = "")
    {
        var now = DateTimeOffset.Now;
        var id = now.ToString("yyyyMMdd-HHmmss-fff");
        return new ReviewHistoryEntry
        {
            Id = id,
            Timestamp = now,
            Branch = branch,
            Base = @base,
            Agent = agent,
            Lang = lang,
            ChangedFiles = changedFiles,
            ReviewMarkdown = reviewMarkdown,
            DiffUnified = diffUnified,
            Repository = repository,
        };
    }
}

/// <summary>
/// File-backed store of past review runs (one JSON file per entry). The default
/// location is the per-user app-data directory, which maps to
/// <c>%APPDATA%</c> on Windows and <c>~/.config</c> on Linux/macOS. The
/// directory is injectable so tests stay hermetic.
/// </summary>
public sealed class HistoryStore
{
    private readonly string _dir;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public HistoryStore(string? directory = null)
    {
        _dir = directory ?? DefaultDirectory();
        System.IO.Directory.CreateDirectory(_dir);
    }

    public static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "coderev-desktop", "history");

    public string Directory => _dir;

    public string Save(ReviewHistoryEntry entry)
    {
        var path = Path.Combine(_dir, entry.Id + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(entry, Options));
        return path;
    }

    /// <summary>All entries, newest first.</summary>
    public IReadOnlyList<ReviewHistoryEntry> List()
    {
        var entries = new List<ReviewHistoryEntry>();
        foreach (var file in System.IO.Directory.EnumerateFiles(_dir, "*.json"))
        {
            try
            {
                var e = JsonSerializer.Deserialize<ReviewHistoryEntry>(File.ReadAllText(file), Options);
                if (e is not null)
                    entries.Add(e);
            }
            catch (JsonException)
            {
                // Skip a corrupt entry rather than failing the whole list.
            }
        }
        entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return entries;
    }

    public ReviewHistoryEntry? Load(string id)
    {
        var path = Path.Combine(_dir, id + ".json");
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<ReviewHistoryEntry>(File.ReadAllText(path), Options);
    }

    public void Delete(string id)
    {
        var path = Path.Combine(_dir, id + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }
}
