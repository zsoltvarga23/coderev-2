// Package ui implements the live console feedback system: step states, a
// spinner, elapsed time, and live streaming of the agent's output. A TTY
// implementation animates; a plain implementation emits line-based output for
// pipes and CI. Both are localized (hu/en).
package ui

import (
	"fmt"
	"io"
	"os"
	"time"

	"github.com/briandowns/spinner"
	isatty "github.com/mattn/go-isatty"
)

// RunInfo describes the run for the opening event/header.
type RunInfo struct {
	Version string
	Branch  string
	Base    string
	Lang    string
}

// MetaInfo carries quantitative data about the review for structured consumers.
type MetaInfo struct {
	ChangedFiles []string
	Hunks        int
	DiffBytes    int
	PromptBytes  int
}

// Reporter receives progress events from the review pipeline. The text
// reporters (TTY/Plain) render a subset for humans; the JSON reporter emits
// every event as NDJSON for the desktop GUI and other structured consumers.
type Reporter interface {
	RunStart(RunInfo)
	StepStart(name string)
	StepInfo(detail string) // extra detail for the current step
	StepDone(d time.Duration)
	StepFail(err error)
	Warn(msg string)
	Meta(MetaInfo)            // quantitative overview (no-op for text reporters)
	Diff(unified string)      // full unified diff (no-op for text reporters)
	BeginStream(title string) // about to stream agent output
	StreamChunk(p []byte)
	Review(markdown string) // full review text (no-op for text reporters)
	Summary(total time.Duration, outPath string)
	Done(exitCode int) // terminal event (no-op for text reporters)
}

type langStrings struct {
	done, total, wrote, warn, streamDefault string
}

func stringsFor(lang string) langStrings {
	if lang == "en" {
		return langStrings{done: "Done", total: "total", wrote: "review written to", warn: "WARNING", streamDefault: "Live response"}
	}
	return langStrings{done: "Kész", total: "összesen", wrote: "review kiírva", warn: "FIGYELEM", streamDefault: "Élő válasz"}
}

// New returns a Reporter for the given format. "json" yields an NDJSON reporter
// for structured consumers (the desktop GUI). Otherwise an animated TTY reporter
// is used on an interactive terminal, or a plain line-based one for pipes/CI or
// when noProgress is set.
func New(out *os.File, lang, format string, noProgress bool) Reporter {
	if format == "json" {
		return NewJSONReporter(out)
	}
	tty := out != nil && isatty.IsTerminal(out.Fd())
	if noProgress || !tty {
		return &PlainReporter{out: out, s: stringsFor(lang)}
	}
	return &TTYReporter{out: out, s: stringsFor(lang)}
}

func fmtDur(d time.Duration) string {
	d = d.Round(time.Second)
	m := int(d / time.Minute)
	sec := int((d % time.Minute) / time.Second)
	if m > 0 {
		return fmt.Sprintf("%d:%02d", m, sec)
	}
	return fmt.Sprintf("%0.1fs", d.Seconds())
}

// ---- TTYReporter -----------------------------------------------------------

// TTYReporter animates the current step with a spinner and streams agent output
// inline. During streaming the spinner is paused so raw text flows cleanly.
type TTYReporter struct {
	out       io.Writer
	s         langStrings
	sp        *spinner.Spinner
	curName   string
	curDetail string
	streaming bool
}

func (r *TTYReporter) RunStart(info RunInfo) {
	title := fmt.Sprintf("coderev %s — PR review: %s  (base: %s)", info.Version, info.Branch, info.Base)
	fmt.Fprintf(r.out, "%s\n\n", title)
}

func (r *TTYReporter) StepStart(name string) {
	r.curName, r.curDetail, r.streaming = name, "", false
	r.sp = spinner.New(spinner.CharSets[14], 100*time.Millisecond)
	r.sp.Writer = r.out
	r.sp.Suffix = "  " + name
	r.sp.Start()
}

func (r *TTYReporter) StepInfo(detail string) {
	r.curDetail = detail
	if r.sp != nil {
		r.sp.Suffix = "  " + r.curName + "   " + detail
	}
}

func (r *TTYReporter) stopSpinner() {
	if r.sp != nil {
		r.sp.Stop()
		r.sp = nil
	}
}

func (r *TTYReporter) StepDone(d time.Duration) {
	r.stopSpinner()
	line := "  ✓ " + r.curName
	if r.curDetail != "" {
		line += "   " + r.curDetail
	}
	fmt.Fprintf(r.out, "%s   (%s)\n", line, fmtDur(d))
}

func (r *TTYReporter) StepFail(err error) {
	r.stopSpinner()
	fmt.Fprintf(r.out, "  ✗ %s — %v\n", r.curName, err)
}

func (r *TTYReporter) Warn(msg string) {
	r.stopSpinner()
	fmt.Fprintf(r.out, "  ! %s: %s\n", r.s.warn, msg)
}

func (r *TTYReporter) BeginStream(title string) {
	r.stopSpinner()
	if title == "" {
		title = r.s.streamDefault
	}
	fmt.Fprintf(r.out, "\n  ── %s ──────────────────────────────\n", title)
	r.streaming = true
}

func (r *TTYReporter) StreamChunk(p []byte) {
	r.out.Write(p)
}

func (r *TTYReporter) Meta(MetaInfo) {} // human detail is conveyed via StepInfo
func (r *TTYReporter) Diff(string)   {}
func (r *TTYReporter) Review(string) {} // already streamed inline
func (r *TTYReporter) Done(int)      {}

func (r *TTYReporter) Summary(total time.Duration, outPath string) {
	r.stopSpinner()
	if outPath != "" {
		fmt.Fprintf(r.out, "\n%s — %s: %s   |   %s %s\n", r.s.done, r.s.wrote, outPath, r.s.total, fmtDur(total))
	} else {
		fmt.Fprintf(r.out, "\n%s   |   %s %s\n", r.s.done, r.s.total, fmtDur(total))
	}
}

// ---- PlainReporter ---------------------------------------------------------

// PlainReporter emits line-based, animation-free output for pipes and CI.
type PlainReporter struct {
	out       io.Writer
	s         langStrings
	curName   string
	curDetail string
}

func (r *PlainReporter) RunStart(info RunInfo) {
	fmt.Fprintf(r.out, "coderev %s — PR review: %s  (base: %s)\n", info.Version, info.Branch, info.Base)
}

func (r *PlainReporter) Meta(MetaInfo) {} // human detail is conveyed via StepInfo
func (r *PlainReporter) Diff(string)   {}
func (r *PlainReporter) Review(string) {} // already streamed inline
func (r *PlainReporter) Done(int)      {}

func (r *PlainReporter) StepStart(name string) {
	r.curName, r.curDetail = name, ""
	fmt.Fprintf(r.out, "  … %s\n", name)
}

func (r *PlainReporter) StepInfo(detail string) { r.curDetail = detail }

func (r *PlainReporter) StepDone(d time.Duration) {
	line := "  ✓ " + r.curName
	if r.curDetail != "" {
		line += "   " + r.curDetail
	}
	fmt.Fprintf(r.out, "%s   (%s)\n", line, fmtDur(d))
}

func (r *PlainReporter) StepFail(err error) {
	fmt.Fprintf(r.out, "  ✗ %s — %v\n", r.curName, err)
}

func (r *PlainReporter) Warn(msg string) {
	fmt.Fprintf(r.out, "  ! %s: %s\n", r.s.warn, msg)
}

func (r *PlainReporter) BeginStream(title string) {
	if title == "" {
		title = r.s.streamDefault
	}
	fmt.Fprintf(r.out, "\n-- %s --\n", title)
}

func (r *PlainReporter) StreamChunk(p []byte) { r.out.Write(p) }

func (r *PlainReporter) Summary(total time.Duration, outPath string) {
	if outPath != "" {
		fmt.Fprintf(r.out, "\n%s — %s: %s (%s %s)\n", r.s.done, r.s.wrote, outPath, r.s.total, fmtDur(total))
	} else {
		fmt.Fprintf(r.out, "\n%s (%s %s)\n", r.s.done, r.s.total, fmtDur(total))
	}
}

// StreamWriter adapts a Reporter to an io.Writer for the agent sink.
func StreamWriter(r Reporter) io.Writer { return streamWriter{r} }

type streamWriter struct{ r Reporter }

func (w streamWriter) Write(p []byte) (int, error) {
	w.r.StreamChunk(p)
	return len(p), nil
}
