using System.Text.Json.Serialization;

namespace CodeRev.Core.Protocol;

/// <summary>
/// One event from the coderev engine's NDJSON (<c>--format json</c>) output.
/// The wire format is flat: every line is a JSON object with a <see cref="Type"/>
/// discriminator and a subset of the optional fields below. Modelling it as a
/// single record (rather than a polymorphic hierarchy) keeps the parser
/// forward-compatible: unknown event types still deserialize, just with their
/// extra fields ignored.
/// </summary>
public sealed record CoderevEvent
{
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("ts")] public long Ts { get; init; }

    [JsonPropertyName("protocol_version")] public int? ProtocolVersion { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("branch")] public string? Branch { get; init; }
    [JsonPropertyName("base")] public string? Base { get; init; }
    [JsonPropertyName("lang")] public string? Lang { get; init; }

    [JsonPropertyName("id")] public int? Id { get; init; }
    [JsonPropertyName("label")] public string? Label { get; init; }
    [JsonPropertyName("detail")] public string? Detail { get; init; }
    [JsonPropertyName("duration_ms")] public long? DurationMs { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }

    [JsonPropertyName("changed_files")] public IReadOnlyList<string>? ChangedFiles { get; init; }
    [JsonPropertyName("hunks")] public int? Hunks { get; init; }
    [JsonPropertyName("diff_bytes")] public int? DiffBytes { get; init; }
    [JsonPropertyName("prompt_bytes")] public int? PromptBytes { get; init; }

    [JsonPropertyName("unified")] public string? Unified { get; init; }
    [JsonPropertyName("chunk")] public string? Chunk { get; init; }
    [JsonPropertyName("markdown")] public string? Markdown { get; init; }

    [JsonPropertyName("out_path")] public string? OutPath { get; init; }
    [JsonPropertyName("total_ms")] public long? TotalMs { get; init; }
    [JsonPropertyName("exit_code")] public int? ExitCode { get; init; }
}

/// <summary>Known event type discriminators (see the desktop PLAN protocol).</summary>
public static class EventType
{
    public const string RunStart = "run_start";
    public const string StepStart = "step_start";
    public const string StepInfo = "step_info";
    public const string StepDone = "step_done";
    public const string StepFail = "step_fail";
    public const string Warn = "warn";
    public const string Meta = "meta";
    public const string Diff = "diff";
    public const string StreamStart = "stream_start";
    public const string Stream = "stream";
    public const string Review = "review";
    public const string Summary = "summary";
    public const string Done = "done";

    /// <summary>The protocol version this client was written against.</summary>
    public const int SupportedProtocolVersion = 1;
}
