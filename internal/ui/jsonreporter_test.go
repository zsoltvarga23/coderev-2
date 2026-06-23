package ui

import (
	"bufio"
	"bytes"
	"encoding/json"
	"errors"
	"strings"
	"testing"
	"time"
)

// decodeEvents parses the NDJSON output into a slice of generic maps.
func decodeEvents(t *testing.T, b *bytes.Buffer) []map[string]any {
	t.Helper()
	var events []map[string]any
	sc := bufio.NewScanner(b)
	sc.Buffer(make([]byte, 0, 64*1024), 1024*1024)
	for sc.Scan() {
		line := strings.TrimSpace(sc.Text())
		if line == "" {
			continue
		}
		var m map[string]any
		if err := json.Unmarshal([]byte(line), &m); err != nil {
			t.Fatalf("invalid NDJSON line %q: %v", line, err)
		}
		events = append(events, m)
	}
	return events
}

func TestJSONReporter_FullSequence(t *testing.T) {
	var buf bytes.Buffer
	r := NewJSONReporter(&buf)

	r.RunStart(RunInfo{Version: "2.0.0", Branch: "feature/x", Base: "origin/main", Lang: "hu"})
	r.StepStart("Diff")
	r.StepInfo("12 fájl · 47 hunk")
	r.StepDone(210 * time.Millisecond)
	r.Meta(MetaInfo{ChangedFiles: []string{"a.go", "b.go"}, Hunks: 47, DiffBytes: 38912, PromptBytes: 9216})
	r.Diff("@@ -1 +1 @@\n-old\n+new")
	r.BeginStream("")
	r.StreamChunk([]byte("## Összegzés\n"))
	r.Review("## Összegzés\nrendben")
	r.Summary(71*time.Second, "review.md")
	r.Done(0)

	events := decodeEvents(t, &buf)

	// Every event must carry a type and a timestamp.
	for i, e := range events {
		if e["type"] == nil {
			t.Errorf("event %d missing type", i)
		}
		if e["ts"] == nil {
			t.Errorf("event %d missing ts", i)
		}
	}

	first := events[0]
	if first["type"] != "run_start" {
		t.Errorf("first event = %v, want run_start", first["type"])
	}
	if first["protocol_version"].(float64) != float64(ProtocolVersion) {
		t.Errorf("run_start protocol_version = %v, want %d", first["protocol_version"], ProtocolVersion)
	}
	if first["branch"] != "feature/x" {
		t.Errorf("run_start branch = %v", first["branch"])
	}

	last := events[len(events)-1]
	if last["type"] != "done" {
		t.Errorf("last event = %v, want done", last["type"])
	}
	if last["exit_code"].(float64) != 0 {
		t.Errorf("done exit_code = %v, want 0", last["exit_code"])
	}

	got := map[string]bool{}
	for _, e := range events {
		got[e["type"].(string)] = true
	}
	for _, want := range []string{"run_start", "step_start", "step_info", "step_done", "meta", "diff", "stream", "review", "summary", "done"} {
		if !got[want] {
			t.Errorf("missing event type %q", want)
		}
	}
}

func TestJSONReporter_StepIDsIncrement(t *testing.T) {
	var buf bytes.Buffer
	r := NewJSONReporter(&buf)
	r.StepStart("a")
	r.StepDone(0)
	r.StepStart("b")
	r.StepFail(errors.New("boom"))

	events := decodeEvents(t, &buf)
	var ids []float64
	for _, e := range events {
		if e["type"] == "step_start" {
			ids = append(ids, e["id"].(float64))
		}
	}
	if len(ids) != 2 || ids[0] != 1 || ids[1] != 2 {
		t.Errorf("step ids = %v, want [1 2]", ids)
	}
	// step_fail must reference the current (second) step.
	for _, e := range events {
		if e["type"] == "step_fail" {
			if e["id"].(float64) != 2 {
				t.Errorf("step_fail id = %v, want 2", e["id"])
			}
			if e["error"] != "boom" {
				t.Errorf("step_fail error = %v, want boom", e["error"])
			}
		}
	}
}

func TestJSONReporter_MetaArray(t *testing.T) {
	var buf bytes.Buffer
	r := NewJSONReporter(&buf)
	r.Meta(MetaInfo{ChangedFiles: []string{"x"}, Hunks: 3})
	events := decodeEvents(t, &buf)
	files := events[0]["changed_files"].([]any)
	if len(files) != 1 || files[0] != "x" {
		t.Errorf("changed_files = %v, want [x]", files)
	}
}
