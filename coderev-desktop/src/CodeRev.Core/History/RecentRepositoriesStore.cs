using System.Text.Json;

namespace CodeRev.Core.History;

/// <summary>A repository the user has opened, for the "recent repositories" list.</summary>
public sealed record RecentRepository
{
    /// <summary>Absolute, normalized path — used to reopen the repo.</summary>
    public string Path { get; init; } = "";

    /// <summary>Display name: the repo's local root folder name.</summary>
    public string Name { get; init; } = "";

    public DateTimeOffset LastOpened { get; init; }
}

/// <summary>
/// A small most-recently-used list of opened repositories, persisted as one JSON
/// file in the per-user app-data directory (next to the review history). Newest
/// first, de-duplicated by path, capped. Distinct from <see cref="HistoryStore"/>
/// (which is per-review): this is per-folder and powers repo autocomplete.
/// </summary>
public sealed class RecentRepositoriesStore
{
    private const int MaxEntries = 20;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    // Repo paths are effectively case-insensitive on Windows; treat them so for
    // de-duplication (the rare Linux case-only difference is acceptable).
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly string _file;

    public RecentRepositoriesStore(string? filePath = null)
    {
        _file = filePath ?? DefaultPath();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_file)!);
    }

    public static string DefaultPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "coderev-desktop", "recent-repositories.json");

    /// <summary>All remembered repositories, newest first.</summary>
    public IReadOnlyList<RecentRepository> List() => Load();

    /// <summary>Records a repository as just-opened: moves it to the front (or
    /// inserts it), de-duplicating by path and capping the list. Best-effort.</summary>
    public void Add(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            return;

        var full = Normalize(repositoryPath);
        var entry = new RecentRepository
        {
            Path = full,
            Name = ReviewHistoryEntry.RepositoryNameFromPath(full),
            LastOpened = DateTimeOffset.Now,
        };

        var list = Load().Where(r => !PathComparer.Equals(r.Path, full)).ToList();
        list.Insert(0, entry);
        if (list.Count > MaxEntries)
            list = list.Take(MaxEntries).ToList();
        Save(list);
    }

    /// <summary>Removes a repository from the list (e.g. it no longer exists).</summary>
    public void Remove(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            return;
        var full = Normalize(repositoryPath);
        var list = Load().Where(r => !PathComparer.Equals(r.Path, full)).ToList();
        Save(list);
    }

    private List<RecentRepository> Load()
    {
        try
        {
            if (!File.Exists(_file))
                return new List<RecentRepository>();
            var list = JsonSerializer.Deserialize<List<RecentRepository>>(File.ReadAllText(_file), Options);
            return list ?? new List<RecentRepository>();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupt, locked, or inaccessible file — degrade to an empty list
            // rather than crashing a caller (e.g. the VM constructor).
            return new List<RecentRepository>();
        }
    }

    private void Save(List<RecentRepository> list)
    {
        try
        {
            File.WriteAllText(_file, JsonSerializer.Serialize(list, Options));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best-effort: never fail the app because the MRU list could not be written.
        }
    }

    /// <summary>Absolute path with any trailing separator removed.</summary>
    public static string Normalize(string path)
    {
        var full = System.IO.Path.GetFullPath(path.Trim());
        return System.IO.Path.TrimEndingDirectorySeparator(full);
    }
}
