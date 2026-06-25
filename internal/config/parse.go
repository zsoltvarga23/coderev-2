package config

import (
	"flag"
	"fmt"
	"io"
	"strings"
)

// Result is the outcome of parsing: the resolved config plus any non-fatal
// warnings (e.g. an unparseable config file) and the config path that was used.
type Result struct {
	Config     Config
	Warnings   []string
	ConfigUsed string
}

// preScan extracts --config/-c and --no-config before the main flag pass, so
// the file can be loaded and its values used as flag defaults.
func preScan(args []string) (configPath string, noConfig bool) {
	for i := 0; i < len(args); i++ {
		a := args[i]
		switch {
		case a == "--no-config":
			noConfig = true
		case a == "--config" || a == "-c":
			if i+1 < len(args) {
				configPath = args[i+1]
				i++
			}
		case strings.HasPrefix(a, "--config="):
			configPath = strings.TrimPrefix(a, "--config=")
		case strings.HasPrefix(a, "-c="):
			configPath = strings.TrimPrefix(a, "-c=")
		}
	}
	return configPath, noConfig
}

// Parse resolves configuration from args, environment, and the config file.
// repoRoot may be empty if not inside a git repo (config discovery still works
// from the current directory). out is where flag usage/errors are written.
func Parse(args []string, getenv func(string) string, repoRoot string, out io.Writer) (*Result, error) {
	configPath, noConfig := preScan(args)

	fc, used, warn := loadFile(configPath, noConfig, repoRoot)
	res := &Result{ConfigUsed: used}
	if warn != "" {
		res.Warnings = append(res.Warnings, warn)
	}

	// Build the resolution chain: defaults -> file -> env. The resulting values
	// become the flag defaults, so any flag set on the CLI overrides them.
	c := Defaults()
	c.applyFile(fc)
	c.applyEnv(getenv)
	c.ConfigPath = configPath
	c.NoConfig = noConfig

	// obey-doc on the CLI is appended to file values, not replaced.
	fileObey := c.ObeyDoc
	c.ObeyDoc = nil
	var cliObey []string

	fs := flag.NewFlagSet("coderev", flag.ContinueOnError)
	fs.SetOutput(out)
	fs.Usage = func() { usage(out, fs) }
	bindFlags(fs, &c, &cliObey)

	positional, err := parseInterspersed(fs, args)
	if err != nil {
		return nil, err
	}

	c.ObeyDoc = append(append([]string{}, fileObey...), cliObey...)

	if len(positional) == 0 {
		return nil, fmt.Errorf("missing required argument: <branch>")
	}
	c.Branch = positional[0]

	if c.Lang != "hu" && c.Lang != "en" {
		return nil, fmt.Errorf("invalid --lang %q: expected hu or en", c.Lang)
	}
	if c.Format != "text" && c.Format != "json" {
		return nil, fmt.Errorf("invalid --format %q: expected text or json", c.Format)
	}

	res.Config = c
	return res, nil
}

// bindFlags registers every shared option on fs, bound to c. obey-doc appends
// into cliObey because the CLI values are concatenated with file values.
func bindFlags(fs *flag.FlagSet, c *Config, cliObey *[]string) {
	// Registered so help lists them; values are handled in preScan.
	fs.String("config", "", "Path to config file (JSON).")
	fs.String("c", "", "Path to config file (JSON) (shorthand).")
	fs.Bool("no-config", false, "Do not load any config file.")

	fs.StringVar(&c.BaseRef, "base-ref", c.BaseRef, "Base reference for the diff.")
	fs.StringVar(&c.HeadRef, "head-ref", c.HeadRef, "Head reference for the diff.")
	fs.Var(stringList{cliObey}, "obey-doc", "Doc the agent must obey (repeatable).")
	fs.StringVar(&c.Template, "template", c.Template, "Result template file.")
	fs.StringVar(&c.Agent, "agent", c.Agent, "Built-in agent: codex|copilot|claude.")
	fs.StringVar(&c.AgentConfig, "agent-config", c.AgentConfig, "Custom agent JSON (overrides --agent).")
	fs.IntVar(&c.ContextLines, "context-lines", c.ContextLines, "Context lines around hunks.")
	fs.BoolVar(&c.IncludeFullFiles, "include-full-files", c.IncludeFullFiles, "Include full changed-file contents.")
	fs.IntVar(&c.MaxDiffBytes, "max-diff-bytes", c.MaxDiffBytes, "Maximum diff size in bytes.")
	fs.IntVar(&c.MaxDocBytes, "max-doc-bytes", c.MaxDocBytes, "Maximum doc/template size in bytes.")
	fs.IntVar(&c.MaxFileBytes, "max-file-bytes", c.MaxFileBytes, "Maximum file size in full-file mode.")
	fs.IntVar(&c.SnippetMaxChars, "snippet-max-chars", c.SnippetMaxChars, "Maximum context snippet length.")
	fs.StringVar(&c.Out, "out", c.Out, "Write agent output to this file.")
	fs.IntVar(&c.AgentTimeout, "agent-timeout", c.AgentTimeout, "Agent timeout in seconds (0 = none).")
	fs.BoolVar(&c.NoProgress, "no-progress", c.NoProgress, "Disable live spinner output.")
	fs.BoolVar(&c.StrictFetch, "strict-fetch", c.StrictFetch, "Treat a failed base fetch as fatal.")
	fs.BoolVar(&c.DryRun, "dry-run", c.DryRun, "Build the prompt but do not run the agent.")
	fs.StringVar(&c.Lang, "lang", c.Lang, "Output/prompt language: hu|en.")
	fs.StringVar(&c.Format, "format", c.Format, "Output format: text|json (json = NDJSON events).")
}

// parseInterspersed parses fs allowing flags and positionals in any order:
// stdlib flag stops at the first non-flag token, so we loop, collect each
// positional, and resume parsing the remainder.
func parseInterspersed(fs *flag.FlagSet, args []string) ([]string, error) {
	var positional []string
	rest := args
	for {
		if err := fs.Parse(rest); err != nil {
			return nil, err
		}
		if fs.NArg() == 0 {
			break
		}
		positional = append(positional, fs.Arg(0))
		rest = fs.Args()[1:]
	}
	return positional, nil
}

func usage(out io.Writer, fs *flag.FlagSet) {
	fmt.Fprintln(out, "coderev — AI-alapú PR review (pluggable agent)")
	fmt.Fprintln(out, "\nHasználat:\n  coderev <branch> [opciók]\n  coderev init [opciók]      # .coderev.json generálása\n  coderev update [--check]   # a CLI frissítése GitHub Release-ből\n\nOpciók:")
	fs.PrintDefaults()
}
