using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeRev.Core.Config;

/// <summary>
/// In-memory model of a <c>.coderev.json</c> file. The JSON shape (kebab-case
/// keys) is identical to what the Go engine reads and what <c>coderev init</c>
/// writes, so the GUI editor and the CLI stay in sync. <see cref="AgentConfig"/>
/// holds the raw JSON of a custom agent (object or string), which the engine
/// accepts in either form.
/// </summary>
public sealed class CoderevConfig
{
    public const string FileName = ".coderev.json";

    [JsonPropertyName("obey-doc")] public List<string> ObeyDoc { get; set; } = new();
    [JsonPropertyName("template")] public string Template { get; set; } = "";
    [JsonPropertyName("include-full-files")] public bool IncludeFullFiles { get; set; }
    [JsonPropertyName("base-ref")] public string BaseRef { get; set; } = "origin/main";
    [JsonPropertyName("head-ref")] public string HeadRef { get; set; } = "HEAD";
    [JsonPropertyName("agent")] public string Agent { get; set; } = "codex";

    [JsonPropertyName("agent-config")]
    [JsonConverter(typeof(RawJsonStringConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentConfig { get; set; }

    [JsonPropertyName("context-lines")] public int ContextLines { get; set; } = 20;
    [JsonPropertyName("max-diff-bytes")] public int MaxDiffBytes { get; set; } = 600000;
    [JsonPropertyName("max-doc-bytes")] public int MaxDocBytes { get; set; } = 200000;
    [JsonPropertyName("max-file-bytes")] public int MaxFileBytes { get; set; } = 200000;
    [JsonPropertyName("snippet-max-chars")] public int SnippetMaxChars { get; set; } = 25000;
    [JsonPropertyName("out")] public string Out { get; set; } = "";
    [JsonPropertyName("agent-timeout")] public int AgentTimeout { get; set; } = 600;
    [JsonPropertyName("no-progress")] public bool NoProgress { get; set; }
    [JsonPropertyName("strict-fetch")] public bool StrictFetch { get; set; }
    [JsonPropertyName("lang")] public string Lang { get; set; } = "hu";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string PathFor(string repoPath) => Path.Combine(repoPath, FileName);

    /// <summary>Loads the config from a repo, or returns defaults if absent.</summary>
    public static CoderevConfig Load(string repoPath)
    {
        var path = PathFor(repoPath);
        if (!File.Exists(path))
            return new CoderevConfig();
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<CoderevConfig>(json, Options) ?? new CoderevConfig();
        cfg.ObeyDoc ??= new List<string>();
        return cfg;
    }

    /// <summary>True if a config file exists in the repo.</summary>
    public static bool Exists(string repoPath) => File.Exists(PathFor(repoPath));

    /// <summary>Serializes the config to JSON (kebab-case, indented).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options) + "\n";

    /// <summary>Writes the config to the repo's .coderev.json and returns the path.</summary>
    public string Save(string repoPath)
    {
        var path = PathFor(repoPath);
        File.WriteAllText(path, ToJson());
        return path;
    }
}

/// <summary>
/// Reads/writes a JSON value that may be a string or an object/array as a raw
/// JSON string, matching the engine's tolerant <c>agent-config</c> handling.
/// </summary>
public sealed class RawJsonStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var el = doc.RootElement;
        return el.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => el.GetString(),
            _ => el.GetRawText(), // object/array → raw JSON text
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            writer.WriteNullValue();
            return;
        }
        // Prefer to emit the value as real JSON (object) when it parses; the
        // engine also accepts a string, so fall back to a string literal.
        try
        {
            using var doc = JsonDocument.Parse(value);
            doc.RootElement.WriteTo(writer);
        }
        catch (JsonException)
        {
            writer.WriteStringValue(value);
        }
    }
}
