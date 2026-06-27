// Command coderev runs an AI-powered PR review over a git branch and reports
// progress live on the console while the agent works.
package main

import (
	"context"
	"errors"
	"flag"
	"fmt"
	"os"
	"os/signal"
	"path/filepath"
	"time"

	"github.com/coderev/coderev/internal/agent"
	"github.com/coderev/coderev/internal/config"
	"github.com/coderev/coderev/internal/diffparse"
	"github.com/coderev/coderev/internal/gitx"
	"github.com/coderev/coderev/internal/prompt"
	"github.com/coderev/coderev/internal/selfupdate"
	"github.com/coderev/coderev/internal/ui"
)

// version is overridable at build time via -ldflags "-X main.version=...".
// Keep in sync with the repo-root VERSION file (CI injects it at build time).
var version = "1.2.0"

func main() {
	os.Exit(run())
}

func run() int {
	// --version short-circuit before any git work.
	for _, a := range os.Args[1:] {
		if a == "--version" || a == "-v" {
			fmt.Printf("coderev %s\n", version)
			return 0
		}
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	defer stop()

	// Resolve repo root early (best-effort) for config discovery.
	repoRoot := ""
	if r, err := gitx.Open(ctx); err == nil {
		repoRoot = r.Root
	}

	// `init` subcommand: generate a .coderev.json and exit.
	if len(os.Args) > 1 && os.Args[1] == "init" {
		return runInit(os.Args[2:], repoRoot)
	}

	// `update` subcommand: self-update the CLI from GitHub Releases and exit.
	if len(os.Args) > 1 && os.Args[1] == "update" {
		return runUpdate(os.Args[2:])
	}

	res, err := config.Parse(os.Args[1:], os.Getenv, repoRoot, os.Stderr)
	if err != nil {
		if errors.Is(err, flag.ErrHelp) {
			return 0 // usage already printed
		}
		fmt.Fprintf(os.Stderr, "ERROR: %v\n", err)
		return 2
	}
	cfg := res.Config

	rep := ui.New(os.Stdout, cfg.Lang, cfg.Format, cfg.NoProgress)
	rep.RunStart(ui.RunInfo{Version: version, Branch: cfg.Branch, Base: cfg.BaseRef, Lang: cfg.Lang})
	for _, w := range res.Warnings {
		rep.Warn(w)
	}

	start := time.Now()

	// 1. Repo.
	rep.StepStart(tr(cfg.Lang, "Repó ellenőrzése", "Checking repository"))
	repo, err := gitx.Open(ctx)
	if err != nil {
		rep.StepFail(err)
		return 2
	}
	rep.StepDone(0)

	// 2. Fetch base (advisory unless --strict-fetch).
	rep.StepStart(tr(cfg.Lang, "Bázis fetch", "Fetching base"))
	if ferr := repo.Fetch(ctx, cfg.BaseRef); ferr != nil {
		if cfg.StrictFetch {
			rep.StepFail(ferr)
			return 2
		}
		rep.StepDone(0)
		rep.Warn(tr(cfg.Lang, "a fetch nem sikerült, elavult bázis lehet", "fetch failed, base may be stale"))
	} else {
		rep.StepDone(0)
	}

	// Resolve refs without mutating the working tree (no checkout). The legacy
	// tool checked out the branch; instead we diff the named branch directly, so
	// it becomes the head ref unless --head-ref overrides the default.
	head := cfg.HeadRef
	if head == "" || head == "HEAD" {
		head = cfg.Branch
	}
	base := cfg.BaseRef
	if err := repo.VerifyRef(ctx, head); err != nil {
		rep.Warn(fmt.Sprintf("%v", err))
	}
	base = repo.MergeBase(ctx, cfg.BaseRef, head)

	// 3. Diff.
	rep.StepStart(tr(cfg.Lang, "Diff számítása", "Computing diff"))
	t := time.Now()
	diff, err := repo.Diff(ctx, base, head)
	if err != nil {
		rep.StepFail(err)
		return 2
	}
	changed, err := repo.ChangedFiles(ctx, base, head)
	if err != nil {
		rep.StepFail(err)
		return 2
	}
	// Parse hunks from the FULL diff before any truncation (bug B1).
	hunks := diffparse.ParseHunks(diff)
	promptDiff := prompt.TruncateBytes(diff, cfg.MaxDiffBytes)
	rep.StepInfo(fmt.Sprintf(tr(cfg.Lang, "%d fájl · %d hunk · %s", "%d files · %d hunks · %s"),
		len(changed), len(hunks), humanBytes(len(diff))))
	rep.StepDone(time.Since(t))

	// 4. Context snippets from the head ref (bug B3).
	rep.StepStart(tr(cfg.Lang, "Kontextus kinyerése", "Extracting context"))
	t = time.Now()
	src := func(path string) (string, bool) { return repo.FileAtRef(ctx, head, path) }
	snippets := diffparse.BuildSnippets(hunks, src, cfg.ContextLines, cfg.SnippetMaxChars)
	rep.StepInfo(fmt.Sprintf(tr(cfg.Lang, "%d részlet", "%d snippets"), len(snippets)))
	rep.StepDone(time.Since(t))

	// 5. Docs, template, full files.
	obeyDocs := readDocs(repo.Root, cfg.ObeyDoc, cfg.MaxDocBytes, rep, cfg.Lang)
	var template *prompt.Doc
	if cfg.Template != "" {
		if d, ok := readDoc(repo.Root, cfg.Template, cfg.MaxDocBytes); ok {
			template = &d
		} else {
			rep.Warn(fmt.Sprintf(tr(cfg.Lang, "sablon nem található: %s", "template not found: %s"), cfg.Template))
		}
	}
	var fullFiles []prompt.Doc
	if cfg.IncludeFullFiles {
		for _, f := range changed {
			if content, ok := repo.FileAtRef(ctx, head, f); ok {
				fullFiles = append(fullFiles, prompt.Doc{Name: f, Content: prompt.TruncateBytes(content, cfg.MaxFileBytes)})
			}
		}
	}

	// 6. Build prompt.
	rep.StepStart(tr(cfg.Lang, "Prompt összeállítása", "Building prompt"))
	p := prompt.Build(prompt.Input{
		Lang:         cfg.Lang,
		Branch:       cfg.Branch,
		BaseRef:      cfg.BaseRef,
		ChangedFiles: changed,
		Diff:         promptDiff,
		ObeyDocs:     obeyDocs,
		Template:     template,
		Snippets:     snippets,
		FullFiles:    fullFiles,
	})
	rep.StepInfo(fmt.Sprintf("~%s", humanBytes(len(p))))
	rep.StepDone(0)

	// Structured overview for GUI/automation consumers (no-op for text).
	rep.Meta(ui.MetaInfo{
		ChangedFiles: changed,
		Hunks:        len(hunks),
		DiffBytes:    len(diff),
		PromptBytes:  len(p),
	})
	rep.Diff(promptDiff)

	// Dry run: print the prompt and stop (gap K6).
	if cfg.DryRun {
		rep.Summary(time.Since(start), "")
		if cfg.Format != "json" {
			fmt.Fprint(os.Stdout, "\n"+p)
		}
		rep.Done(0)
		return 0
	}

	// 7. Run the agent with live streaming.
	spec, err := agent.LoadSpec(cfg.Agent, cfg.AgentConfig)
	if err != nil {
		rep.Warn(err.Error())
		return 2
	}
	agentCtx := ctx
	if cfg.AgentTimeout > 0 {
		var cancel context.CancelFunc
		agentCtx, cancel = context.WithTimeout(ctx, time.Duration(cfg.AgentTimeout)*time.Second)
		defer cancel()
	}
	rep.StepStart(fmt.Sprintf(tr(cfg.Lang, "AI review (%s)", "AI review (%s)"), spec.Name))
	rep.BeginStream("")
	output, aerr := agent.Run(agentCtx, spec, p, repo.Root, ui.StreamWriter(rep))
	if aerr != nil {
		rep.StepFail(aerr)
		return 1
	}
	rep.StepDone(time.Since(start))
	rep.Review(output)

	// 8. Output file.
	outPath := ""
	if cfg.Out != "" {
		outPath = cfg.Out
		if !filepath.IsAbs(outPath) {
			outPath = filepath.Join(repo.Root, outPath)
		}
		if err := os.MkdirAll(filepath.Dir(outPath), 0o755); err == nil {
			if werr := os.WriteFile(outPath, []byte(output), 0o644); werr != nil {
				rep.Warn(fmt.Sprintf("%v", werr))
				outPath = ""
			}
		}
	}

	rep.Summary(time.Since(start), outPath)
	rep.Done(0)
	return 0
}

// runInit handles the `init` subcommand: generate a .coderev.json populated
// from the provided options (defaults for the rest) into the repo root.
func runInit(args []string, repoRoot string) int {
	res, err := config.ParseInit(args, repoRoot, os.Stderr)
	if err != nil {
		if errors.Is(err, flag.ErrHelp) {
			return 0
		}
		fmt.Fprintf(os.Stderr, "ERROR: %v\n", err)
		return 2
	}
	data, err := config.GenerateJSON(res.Config)
	if err != nil {
		fmt.Fprintf(os.Stderr, "ERROR: %v\n", err)
		return 2
	}
	if err := config.WriteConfigFile(res.Path, data, res.Force); err != nil {
		fmt.Fprintf(os.Stderr, "ERROR: %v\n", err)
		return 1
	}
	fmt.Printf("%s %s\n", tr(res.Config.Lang, "Konfiguráció kiírva:", "Wrote config:"), res.Path)
	return 0
}

// runUpdate handles the `update` subcommand: check GitHub Releases for a newer
// CLI build and, unless --check is given, download (with checksum verification)
// and replace the running binary.
func runUpdate(args []string) int {
	checkOnly := false
	for _, a := range args {
		switch a {
		case "--check", "-check":
			checkOnly = true
		case "-h", "--help":
			fmt.Println("Usage: coderev update [--check]")
			fmt.Println("  Updates the coderev CLI from GitHub Releases.")
			fmt.Println("  --check   only report whether a newer version exists")
			return 0
		}
	}
	if err := selfupdate.Run(selfupdate.Options{
		CurrentVersion: version,
		CheckOnly:      checkOnly,
	}); err != nil {
		fmt.Fprintf(os.Stderr, "ERROR: %v\n", err)
		return 1
	}
	return 0
}

// tr selects a Hungarian or English string.
func tr(lang, hu, en string) string {
	if lang == "en" {
		return en
	}
	return hu
}

func readDocs(root string, paths []string, maxBytes int, rep ui.Reporter, lang string) []prompt.Doc {
	var docs []prompt.Doc
	for _, p := range paths {
		if d, ok := readDoc(root, p, maxBytes); ok {
			docs = append(docs, d)
		} else {
			rep.Warn(fmt.Sprintf(tr(lang, "dokumentum nem található: %s", "doc not found: %s"), p))
		}
	}
	return docs
}

func readDoc(root, p string, maxBytes int) (prompt.Doc, bool) {
	abs := p
	if !filepath.IsAbs(abs) {
		abs = filepath.Join(root, p)
	}
	data, err := os.ReadFile(abs)
	if err != nil {
		return prompt.Doc{}, false
	}
	return prompt.Doc{Name: p, Content: prompt.TruncateBytes(string(data), maxBytes)}, true
}

func humanBytes(n int) string {
	switch {
	case n >= 1<<20:
		return fmt.Sprintf("%.1f MB", float64(n)/(1<<20))
	case n >= 1<<10:
		return fmt.Sprintf("%d KB", n/(1<<10))
	default:
		return fmt.Sprintf("%d B", n)
	}
}
