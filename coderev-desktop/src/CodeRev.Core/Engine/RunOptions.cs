namespace CodeRev.Core.Engine;

/// <summary>
/// Options for one engine invocation. Mirrors the coderev CLI flags the GUI
/// needs; the runner always adds <c>--format json</c>.
/// </summary>
public sealed record RunOptions
{
    /// <summary>Branch to review (positional argument). Required.</summary>
    public required string Branch { get; init; }

    /// <summary>Working directory (the git repository) to run the engine in.</summary>
    public required string RepositoryPath { get; init; }

    // base/agent/lang are only forwarded when set, so an unset GUI field leaves
    // the engine free to use the repo's .coderev.json (CLI would otherwise win).
    public string? BaseRef { get; init; }
    public string? HeadRef { get; init; }
    public string? Agent { get; init; }
    public string? Lang { get; init; }
    public bool IncludeFullFiles { get; init; }
    public bool DryRun { get; init; }
    public string? OutFile { get; init; }

    /// <summary>Builds the engine argument list (excluding the executable).</summary>
    public IReadOnlyList<string> ToArguments()
    {
        var args = new List<string> { Branch, "--format", "json" };
        void Add(string flag, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                args.Add(flag);
                args.Add(value!);
            }
        }
        Add("--base-ref", BaseRef);
        Add("--head-ref", HeadRef);
        Add("--agent", Agent);
        Add("--lang", Lang);
        Add("--out", OutFile);
        if (IncludeFullFiles) args.Add("--include-full-files");
        if (DryRun) args.Add("--dry-run");
        return args;
    }
}
