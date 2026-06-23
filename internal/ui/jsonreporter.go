package ui

import (
	"encoding/json"
	"io"
	"sync"
	"time"
)

// ProtocolVersion identifies the NDJSON event schema. Consumers should tolerate
// unknown event types for forward compatibility; bump this on breaking changes.
const ProtocolVersion = 1

// JSONReporter emits the review pipeline as newline-delimited JSON (NDJSON), one
// event object per line, for structured consumers such as the desktop GUI.
// It implements Reporter; the human-oriented StepInfo detail is preserved as a
// step_info event so clients can show it verbatim.
type JSONReporter struct {
	mu     sync.Mutex
	out    io.Writer
	enc    *json.Encoder
	stepID int
	curID  int
}

// NewJSONReporter returns a JSONReporter writing to out.
func NewJSONReporter(out io.Writer) *JSONReporter {
	return &JSONReporter{out: out, enc: json.NewEncoder(out)}
}

// event is the wire shape. Optional fields use omitempty so each line stays
// minimal and self-describing.
type event struct {
	Type            string   `json:"type"`
	Ts              int64    `json:"ts"`
	ProtocolVersion int      `json:"protocol_version,omitempty"`
	Version         string   `json:"version,omitempty"`
	Branch          string   `json:"branch,omitempty"`
	Base            string   `json:"base,omitempty"`
	Lang            string   `json:"lang,omitempty"`
	ID              int      `json:"id,omitempty"`
	Label           string   `json:"label,omitempty"`
	Detail          string   `json:"detail,omitempty"`
	DurationMs      int64    `json:"duration_ms,omitempty"`
	Error           string   `json:"error,omitempty"`
	Message         string   `json:"message,omitempty"`
	ChangedFiles    []string `json:"changed_files,omitempty"`
	Hunks           int      `json:"hunks,omitempty"`
	DiffBytes       int      `json:"diff_bytes,omitempty"`
	PromptBytes     int      `json:"prompt_bytes,omitempty"`
	Unified         string   `json:"unified,omitempty"`
	Chunk           string   `json:"chunk,omitempty"`
	Markdown        string   `json:"markdown,omitempty"`
	OutPath         string   `json:"out_path,omitempty"`
	TotalMs         int64    `json:"total_ms,omitempty"`
	ExitCode        *int     `json:"exit_code,omitempty"`
}

func nowMs() int64 { return time.Now().UnixMilli() }

// emit writes one event line under the lock.
func (r *JSONReporter) emit(e event) {
	e.Ts = nowMs()
	r.mu.Lock()
	defer r.mu.Unlock()
	_ = r.enc.Encode(e) // Encode appends '\n'
}

func (r *JSONReporter) RunStart(info RunInfo) {
	r.emit(event{
		Type:            "run_start",
		ProtocolVersion: ProtocolVersion,
		Version:         info.Version,
		Branch:          info.Branch,
		Base:            info.Base,
		Lang:            info.Lang,
	})
}

func (r *JSONReporter) StepStart(name string) {
	r.stepID++
	r.curID = r.stepID
	r.emit(event{Type: "step_start", ID: r.curID, Label: name})
}

func (r *JSONReporter) StepInfo(detail string) {
	r.emit(event{Type: "step_info", ID: r.curID, Detail: detail})
}

func (r *JSONReporter) StepDone(d time.Duration) {
	r.emit(event{Type: "step_done", ID: r.curID, DurationMs: d.Milliseconds()})
}

func (r *JSONReporter) StepFail(err error) {
	msg := ""
	if err != nil {
		msg = err.Error()
	}
	r.emit(event{Type: "step_fail", ID: r.curID, Error: msg})
}

func (r *JSONReporter) Warn(msg string) {
	r.emit(event{Type: "warn", Message: msg})
}

func (r *JSONReporter) Meta(m MetaInfo) {
	r.emit(event{
		Type:         "meta",
		ChangedFiles: m.ChangedFiles,
		Hunks:        m.Hunks,
		DiffBytes:    m.DiffBytes,
		PromptBytes:  m.PromptBytes,
	})
}

func (r *JSONReporter) Diff(unified string) {
	if unified == "" {
		return
	}
	r.emit(event{Type: "diff", Unified: unified})
}

func (r *JSONReporter) BeginStream(string) {
	r.emit(event{Type: "stream_start"})
}

func (r *JSONReporter) StreamChunk(p []byte) {
	r.emit(event{Type: "stream", Chunk: string(p)})
}

func (r *JSONReporter) Review(markdown string) {
	r.emit(event{Type: "review", Markdown: markdown})
}

func (r *JSONReporter) Summary(total time.Duration, outPath string) {
	r.emit(event{Type: "summary", TotalMs: total.Milliseconds(), OutPath: outPath})
}

func (r *JSONReporter) Done(exitCode int) {
	code := exitCode
	r.emit(event{Type: "done", ExitCode: &code})
}
