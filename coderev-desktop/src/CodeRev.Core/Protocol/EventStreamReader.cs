using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CodeRev.Core.Protocol;

/// <summary>
/// Parses an NDJSON event stream from the coderev engine into typed
/// <see cref="CoderevEvent"/> values. It is deliberately tolerant: blank lines
/// are skipped and malformed lines are reported via <see cref="OnParseError"/>
/// rather than aborting the stream, so a single bad line cannot kill a review.
/// </summary>
public sealed class EventStreamReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Raised for each line that fails to parse (line text, exception).</summary>
    public event Action<string, Exception>? OnParseError;

    /// <summary>
    /// Tries to parse a single NDJSON line. Returns false for blank lines or
    /// malformed JSON (in which case <paramref name="ev"/> is null).
    /// </summary>
    public static bool TryParseLine(string line, out CoderevEvent? ev)
    {
        ev = null;
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
            return false;
        try
        {
            ev = JsonSerializer.Deserialize<CoderevEvent>(trimmed, Options);
        }
        catch (JsonException)
        {
            return false;
        }
        if (ev is null || string.IsNullOrEmpty(ev.Type))
        {
            ev = null;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Reads the stream line by line and yields each parsed event as it arrives,
    /// enabling live UI updates. Honors cancellation between lines.
    /// </summary>
    public async IAsyncEnumerable<CoderevEvent> ReadAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                yield break; // EOF

            if (line.Trim().Length == 0)
                continue;

            CoderevEvent? ev;
            try
            {
                if (!TryParseLine(line, out ev) || ev is null)
                    continue;
            }
            catch (Exception ex)
            {
                OnParseError?.Invoke(line, ex);
                continue;
            }
            yield return ev;
        }
    }
}
