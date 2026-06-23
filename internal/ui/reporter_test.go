package ui

import (
	"bytes"
	"errors"
	"strings"
	"testing"
	"time"
)

func plain(buf *bytes.Buffer, lang string) *PlainReporter {
	return &PlainReporter{out: buf, s: stringsFor(lang)}
}

func TestPlainReporter_StepLifecycle(t *testing.T) {
	var buf bytes.Buffer
	r := plain(&buf, "hu")
	r.RunStart(RunInfo{Version: "v2", Branch: "feature/x", Base: "origin/main"})
	r.StepStart("Diff számítása")
	r.StepInfo("12 fájl · 47 hunk")
	r.StepDone(200 * time.Millisecond)

	out := buf.String()
	for _, want := range []string{"feature/x", "Diff számítása", "12 fájl · 47 hunk", "✓"} {
		if !strings.Contains(out, want) {
			t.Errorf("output missing %q:\n%s", want, out)
		}
	}
}

func TestPlainReporter_Fail(t *testing.T) {
	var buf bytes.Buffer
	r := plain(&buf, "hu")
	r.StepStart("Bázis fetch")
	r.StepFail(errors.New("network down"))
	if !strings.Contains(buf.String(), "✗") || !strings.Contains(buf.String(), "network down") {
		t.Errorf("fail output = %q", buf.String())
	}
}

func TestPlainReporter_Stream(t *testing.T) {
	var buf bytes.Buffer
	r := plain(&buf, "hu")
	r.BeginStream("")
	w := StreamWriter(r)
	w.Write([]byte("## Summary\n"))
	w.Write([]byte("looks good"))
	out := buf.String()
	if !strings.Contains(out, "Élő válasz") {
		t.Errorf("missing localized stream title:\n%s", out)
	}
	if !strings.Contains(out, "## Summary") || !strings.Contains(out, "looks good") {
		t.Errorf("stream content missing:\n%s", out)
	}
}

func TestPlainReporter_Summary(t *testing.T) {
	var buf bytes.Buffer
	r := plain(&buf, "en")
	r.Summary(71*time.Second, "review.md")
	out := buf.String()
	if !strings.Contains(out, "review written to") || !strings.Contains(out, "review.md") {
		t.Errorf("summary missing out path:\n%s", out)
	}
	if !strings.Contains(out, "1:11") {
		t.Errorf("summary missing duration 1:11:\n%s", out)
	}
}

func TestFmtDur(t *testing.T) {
	cases := map[time.Duration]string{
		200 * time.Millisecond:  "0.0s",
		1500 * time.Millisecond: "2.0s", // rounds to nearest second
		71 * time.Second:        "1:11",
		600 * time.Second:       "10:00",
	}
	for d, want := range cases {
		if got := fmtDur(d); got != want {
			t.Errorf("fmtDur(%v) = %q, want %q", d, got, want)
		}
	}
}

func TestNew_PlainWhenNoProgress(t *testing.T) {
	r := New(nil, "hu", "text", true)
	if _, ok := r.(*PlainReporter); !ok {
		t.Errorf("New with noProgress should return *PlainReporter, got %T", r)
	}
}

func TestNew_JSONFormat(t *testing.T) {
	r := New(nil, "hu", "json", false)
	if _, ok := r.(*JSONReporter); !ok {
		t.Errorf("New with format=json should return *JSONReporter, got %T", r)
	}
}
