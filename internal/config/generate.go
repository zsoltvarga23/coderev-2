package config

import (
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"path/filepath"
)

// InitResult is the outcome of parsing an `init` invocation.
type InitResult struct {
	Config Config
	Path   string // target file to write (absolute)
	Force  bool   // overwrite an existing file
}

// ParseInit parses the flags for the `init` subcommand. Unlike Parse it does
// not consult the environment or an existing config file (the output must be
// predictable), does not require a branch, and supports an optional positional
// path plus a --force flag. repoRoot anchors the default target (.coderev.json
// in the repo root, or the current directory if not in a repo).
func ParseInit(args []string, repoRoot string, out io.Writer) (*InitResult, error) {
	c := Defaults()
	var cliObey []string

	fs := flag.NewFlagSet("coderev init", flag.ContinueOnError)
	fs.SetOutput(out)
	fs.Usage = func() {
		fmt.Fprintln(out, "coderev init — .coderev.json generálása a megadott opciókkal")
		fmt.Fprintln(out, "\nHasználat:\n  coderev init [útvonal] [opciók]\n\nOpciók:")
		fs.PrintDefaults()
	}
	bindFlags(fs, &c, &cliObey)
	force := fs.Bool("force", false, "Overwrite an existing config file.")

	positional, err := parseInterspersed(fs, args)
	if err != nil {
		return nil, err
	}
	c.ObeyDoc = cliObey

	if c.Lang != "hu" && c.Lang != "en" {
		return nil, fmt.Errorf("invalid --lang %q: expected hu or en", c.Lang)
	}

	base := repoRoot
	if base == "" {
		base, _ = os.Getwd()
	}
	target := filepath.Join(base, ".coderev.json")
	if len(positional) > 0 {
		p := positional[0]
		if !filepath.IsAbs(p) {
			p = filepath.Join(base, p)
		}
		target = p
	}

	return &InitResult{Config: c, Path: target, Force: *force}, nil
}

// outSchema is the JSON shape written by `init`. Field order is preserved and
// keys are kebab-case, matching the format read by loadFile.
type outSchema struct {
	ObeyDoc          []string `json:"obey-doc"`
	Template         string   `json:"template"`
	IncludeFullFiles bool     `json:"include-full-files"`
	BaseRef          string   `json:"base-ref"`
	HeadRef          string   `json:"head-ref"`
	Agent            string   `json:"agent"`
	AgentConfig      string   `json:"agent-config,omitempty"`
	ContextLines     int      `json:"context-lines"`
	MaxDiffBytes     int      `json:"max-diff-bytes"`
	MaxDocBytes      int      `json:"max-doc-bytes"`
	MaxFileBytes     int      `json:"max-file-bytes"`
	SnippetMaxChars  int      `json:"snippet-max-chars"`
	Out              string   `json:"out"`
	AgentTimeout     int      `json:"agent-timeout"`
	NoProgress       bool     `json:"no-progress"`
	StrictFetch      bool     `json:"strict-fetch"`
	Lang             string   `json:"lang"`
}

// GenerateJSON renders c as an indented .coderev.json document.
func GenerateJSON(c Config) ([]byte, error) {
	obey := c.ObeyDoc
	if obey == nil {
		obey = []string{}
	}
	s := outSchema{
		ObeyDoc:          obey,
		Template:         c.Template,
		IncludeFullFiles: c.IncludeFullFiles,
		BaseRef:          c.BaseRef,
		HeadRef:          c.HeadRef,
		Agent:            c.Agent,
		AgentConfig:      c.AgentConfig,
		ContextLines:     c.ContextLines,
		MaxDiffBytes:     c.MaxDiffBytes,
		MaxDocBytes:      c.MaxDocBytes,
		MaxFileBytes:     c.MaxFileBytes,
		SnippetMaxChars:  c.SnippetMaxChars,
		Out:              c.Out,
		AgentTimeout:     c.AgentTimeout,
		NoProgress:       c.NoProgress,
		StrictFetch:      c.StrictFetch,
		Lang:             c.Lang,
	}
	b, err := json.MarshalIndent(s, "", "  ")
	if err != nil {
		return nil, err
	}
	return append(b, '\n'), nil
}

// WriteConfigFile writes the generated config to path. It refuses to overwrite
// an existing file unless force is true (avoids the legacy silent-clobber, K8).
func WriteConfigFile(path string, data []byte, force bool) error {
	if !force {
		if _, err := os.Stat(path); err == nil {
			return fmt.Errorf("file already exists: %s (use --force to overwrite)", path)
		}
	}
	if dir := filepath.Dir(path); dir != "" {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return err
		}
	}
	return os.WriteFile(path, data, 0o644)
}
