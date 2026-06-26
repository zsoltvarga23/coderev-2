using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using CodeRev.Core.Protocol;

namespace CodeRev.Core.Engine;

/// <summary>Runs the coderev engine and yields its events.</summary>
public interface ICoderevRunner
{
    /// <summary>
    /// Starts the engine and yields each NDJSON event as it arrives. Cancelling
    /// the token kills the engine process (this maps to the GUI Stop button).
    /// </summary>
    IAsyncEnumerable<CoderevEvent> RunAsync(RunOptions options, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="ICoderevRunner"/>: spawns the engine binary as a child
/// process and parses its stdout via <see cref="EventStreamReader"/>. The
/// process boundary keeps engine faults from crashing the GUI and makes
/// cancellation a clean process kill.
/// </summary>
public sealed class CoderevRunner : ICoderevRunner
{
    private readonly string _executable;
    private readonly EventStreamReader _reader = new();

    /// <param name="executablePath">
    /// Path to the engine binary; defaults to <see cref="BinaryLocator.Resolve"/>.
    /// </param>
    public CoderevRunner(string? executablePath = null)
    {
        _executable = executablePath ?? BinaryLocator.Resolve();
    }

    /// <summary>Surfaced parse errors from the underlying stream reader.</summary>
    public event Action<string, Exception>? OnParseError
    {
        add => _reader.OnParseError += value;
        remove => _reader.OnParseError -= value;
    }

    public async IAsyncEnumerable<CoderevEvent> RunAsync(
        RunOptions options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _executable,
            WorkingDirectory = options.RepositoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // The Go engine writes UTF-8; without this the runtime decodes stdout
            // with the OS console code page, garbling non-ASCII text (e.g. the
            // Hungarian review). Force UTF-8 on both pipes.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in options.ToArguments())
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"failed to start engine: {_executable}");

        // Drain stderr concurrently so the engine never blocks on a full pipe.
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await foreach (var ev in _reader.ReadAsync(process.StandardOutput, ct).ConfigureAwait(false))
                yield return ev;
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch { /* already gone */ }
            }
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0 && !ct.IsCancellationRequested)
        {
            // The engine already emitted step_fail/warn events; include stderr
            // for diagnostics the protocol did not carry.
            throw new EngineFailedException(process.ExitCode, stderr.Trim());
        }
    }
}

/// <summary>Thrown when the engine exits non-zero (outside cancellation).</summary>
public sealed class EngineFailedException(int exitCode, string stderr)
    : Exception($"engine exited with code {exitCode}: {stderr}")
{
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}
