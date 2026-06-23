package agent

import (
	"bytes"
	"context"
	"fmt"
	"io"
	"os"
	"strings"
	"testing"
	"time"
)

// TestHelperProcess is not a real test; it is re-executed as the "agent"
// subprocess. Behavior is selected by the first arg after "--".
func TestHelperProcess(t *testing.T) {
	if os.Getenv("GO_WANT_HELPER_PROCESS") != "1" {
		return
	}
	args := os.Args
	for len(args) > 0 && args[0] != "--" {
		args = args[1:]
	}
	if len(args) > 0 {
		args = args[1:] // drop "--"
	}
	if len(args) == 0 {
		os.Exit(3)
	}
	mode, rest := args[0], args[1:]
	stdin, _ := io.ReadAll(os.Stdin)

	switch mode {
	case "echo-stdin":
		os.Stdout.Write(stdin)
	case "echo-args":
		if len(stdin) > 0 {
			fmt.Print("UNEXPECTED-STDIN ")
		}
		fmt.Print(strings.Join(rest, " "))
	case "echo-file":
		if len(stdin) > 0 {
			fmt.Print("UNEXPECTED-STDIN ")
		}
		data, _ := os.ReadFile(rest[0])
		os.Stdout.Write(data)
	case "sleep":
		time.Sleep(2 * time.Second)
	case "fail":
		fmt.Fprint(os.Stderr, "boom")
		os.Exit(1)
	}
	os.Exit(0)
}

func helperSpec(t *testing.T, mode Mode, extra ...string) Spec {
	t.Helper()
	self, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	cmd := append([]string{self, "-test.run=TestHelperProcess", "--"}, extra...)
	return Spec{
		Name: "helper",
		Cmd:  cmd,
		Mode: mode,
		Cwd:  ".",
		Env:  map[string]string{"GO_WANT_HELPER_PROCESS": "1"},
	}
}

func TestRun_StdinMode(t *testing.T) {
	spec := helperSpec(t, ModeStdin, "echo-stdin")
	var sink bytes.Buffer
	out, err := Run(context.Background(), spec, "HELLO-PROMPT", t.TempDir(), &sink)
	if err != nil {
		t.Fatal(err)
	}
	if out != "HELLO-PROMPT" {
		t.Errorf("collected = %q, want HELLO-PROMPT", out)
	}
	if sink.String() != "HELLO-PROMPT" {
		t.Errorf("streamed = %q, want HELLO-PROMPT", sink.String())
	}
}

func TestRun_ArgMode_NoStdin(t *testing.T) {
	spec := helperSpec(t, ModeArg, "echo-args", "{prompt}")
	out, err := Run(context.Background(), spec, "ARGPROMPT", t.TempDir(), io.Discard)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(out, "UNEXPECTED-STDIN") {
		t.Error("arg mode must not feed the prompt to stdin (bug B2)")
	}
	if !strings.Contains(out, "ARGPROMPT") {
		t.Errorf("arg mode output = %q, want it to contain ARGPROMPT", out)
	}
}

func TestRun_FileMode_NoStdin(t *testing.T) {
	spec := helperSpec(t, ModeFile, "echo-file", "{prompt_file}")
	out, err := Run(context.Background(), spec, "FILEPROMPT", t.TempDir(), io.Discard)
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(out, "UNEXPECTED-STDIN") {
		t.Error("file mode must not feed the prompt to stdin (bug B2)")
	}
	if !strings.Contains(out, "FILEPROMPT") {
		t.Errorf("file mode output = %q, want it to contain FILEPROMPT", out)
	}
}

func TestRun_Timeout(t *testing.T) {
	spec := helperSpec(t, ModeStdin, "sleep")
	ctx, cancel := context.WithTimeout(context.Background(), 150*time.Millisecond)
	defer cancel()
	_, err := Run(ctx, spec, "x", t.TempDir(), io.Discard)
	if err == nil || !strings.Contains(err.Error(), "timed out") {
		t.Errorf("expected timeout error, got %v", err)
	}
}

func TestRun_Failure(t *testing.T) {
	spec := helperSpec(t, ModeStdin, "fail")
	_, err := Run(context.Background(), spec, "x", t.TempDir(), io.Discard)
	if err == nil || !strings.Contains(err.Error(), "boom") {
		t.Errorf("expected failure with stderr 'boom', got %v", err)
	}
}

func TestLoadSpec_Preset(t *testing.T) {
	s, err := LoadSpec("codex", "")
	if err != nil {
		t.Fatal(err)
	}
	if s.Mode != ModeStdin || s.Cmd[0] != "codex" {
		t.Errorf("codex preset = %+v", s)
	}
}

func TestLoadSpec_ClaudePreset(t *testing.T) {
	s, err := LoadSpec("claude", "")
	if err != nil {
		t.Fatal(err)
	}
	if s.Mode != ModeStdin || s.Cmd[0] != "claude" {
		t.Errorf("claude preset = %+v", s)
	}
}

func TestLoadSpec_Unknown(t *testing.T) {
	if _, err := LoadSpec("nope", ""); err == nil {
		t.Error("expected error for unknown agent")
	}
}

func TestLoadSpec_JSONOverride(t *testing.T) {
	cfg := `{"name":"mycli","mode":"file","cmd":"mycli review --in {prompt_file}"}`
	s, err := LoadSpec("codex", cfg)
	if err != nil {
		t.Fatal(err)
	}
	if s.Name != "mycli" || s.Mode != ModeFile {
		t.Errorf("override = %+v", s)
	}
	want := []string{"mycli", "review", "--in", "{prompt_file}"}
	if strings.Join(s.Cmd, " ") != strings.Join(want, " ") {
		t.Errorf("cmd = %v, want %v", s.Cmd, want)
	}
}

func TestLoadSpec_JSONArrayCmd(t *testing.T) {
	s, err := LoadSpec("x", `{"cmd":["a","b","c"]}`)
	if err != nil {
		t.Fatal(err)
	}
	if len(s.Cmd) != 3 || s.Mode != ModeStdin {
		t.Errorf("array cmd spec = %+v", s)
	}
}
