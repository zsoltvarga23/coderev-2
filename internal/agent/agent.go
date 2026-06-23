// Package agent runs a pluggable external AI agent (Codex, Copilot, or any CLI)
// and streams its output. Unlike the legacy tool, it sends the prompt on stdin
// only in stdin mode (bug B2), enforces a timeout (bug R3), and streams output
// as it arrives rather than buffering the whole response (bug R4).
package agent

import (
	"bufio"
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
)

// Mode is how the prompt is delivered to the agent process.
type Mode string

const (
	ModeStdin Mode = "stdin" // prompt written to the process stdin
	ModeArg   Mode = "arg"   // {prompt} placeholder replaced in args
	ModeFile  Mode = "file"  // prompt written to a temp file, {prompt_file} replaced
)

// Spec describes how to invoke an agent.
type Spec struct {
	Name string
	Cmd  []string
	Mode Mode
	Cwd  string // relative to repo root
	Env  map[string]string
}

// rawSpec is the JSON shape accepted from --agent-config.
type rawSpec struct {
	Name string            `json:"name"`
	Cmd  json.RawMessage   `json:"cmd"` // string or []string
	Mode string            `json:"mode"`
	Cwd  string            `json:"cwd"`
	Env  map[string]string `json:"env"`
}

var presets = map[string]Spec{
	"codex":   {Name: "codex", Cmd: []string{"codex", "exec", "-"}, Mode: ModeStdin},
	"copilot": {Name: "copilot", Cmd: []string{"copilot", "--prompt-file", "{prompt_file}"}, Mode: ModeFile},
	// Claude Code CLI in headless print mode; the prompt is piped on stdin so
	// large diffs are not subject to command-line length limits. Requires the
	// standalone CLI (npm i -g @anthropic-ai/claude-code), not just the IDE plugin.
	"claude": {Name: "claude", Cmd: []string{"claude", "-p"}, Mode: ModeStdin},
}

// LoadSpec resolves an agent spec from a preset name or a JSON override.
func LoadSpec(name, agentConfig string) (Spec, error) {
	if strings.TrimSpace(agentConfig) != "" {
		var raw rawSpec
		if err := json.Unmarshal([]byte(agentConfig), &raw); err != nil {
			return Spec{}, fmt.Errorf("invalid --agent-config JSON: %w", err)
		}
		cmd, err := decodeCmd(raw.Cmd)
		if err != nil {
			return Spec{}, err
		}
		if len(cmd) == 0 {
			return Spec{}, errors.New("agent-config: cmd is empty")
		}
		mode := Mode(raw.Mode)
		if mode == "" {
			mode = ModeStdin
		}
		cwd := raw.Cwd
		if cwd == "" {
			cwd = "."
		}
		nm := raw.Name
		if nm == "" {
			nm = name
		}
		return Spec{Name: nm, Cmd: cmd, Mode: mode, Cwd: cwd, Env: raw.Env}, nil
	}
	p, ok := presets[name]
	if !ok {
		return Spec{}, fmt.Errorf("unknown agent %q: use codex|copilot|claude or provide --agent-config", name)
	}
	return p, nil
}

// decodeCmd accepts either a JSON string (shell-split) or a JSON array.
func decodeCmd(raw json.RawMessage) ([]string, error) {
	if len(raw) == 0 {
		return nil, nil
	}
	var arr []string
	if err := json.Unmarshal(raw, &arr); err == nil {
		return arr, nil
	}
	var s string
	if err := json.Unmarshal(raw, &s); err == nil {
		return shellSplit(s), nil
	}
	return nil, errors.New("agent-config: cmd must be a string or array of strings")
}

// Run executes the agent and streams stdout to sink as it arrives. It returns
// the full collected output (for saving to --out). repoRoot anchors a relative
// Cwd. The context governs timeout and cancellation.
func Run(ctx context.Context, spec Spec, prompt, repoRoot string, sink io.Writer) (string, error) {
	if len(spec.Cmd) == 0 {
		return "", errors.New("agent cmd is empty")
	}

	args := append([]string{}, spec.Cmd...)
	exe, err := exec.LookPath(args[0])
	if err != nil {
		return "", fmt.Errorf("command not found on PATH: %s", args[0])
	}
	args[0] = exe

	var tmpFile string
	switch spec.Mode {
	case ModeArg:
		for i := range args {
			args[i] = strings.ReplaceAll(args[i], "{prompt}", prompt)
		}
	case ModeFile:
		f, err := os.CreateTemp("", "coderev-prompt-*.md")
		if err != nil {
			return "", fmt.Errorf("creating prompt temp file: %w", err)
		}
		tmpFile = f.Name()
		defer os.Remove(tmpFile)
		if _, err := f.WriteString(prompt); err != nil {
			f.Close()
			return "", err
		}
		f.Close()
		for i := range args {
			args[i] = strings.ReplaceAll(args[i], "{prompt_file}", tmpFile)
		}
	case ModeStdin:
		// handled below
	default:
		return "", fmt.Errorf("unknown agent mode %q: expected stdin|arg|file", spec.Mode)
	}

	args = wrapForWindows(args)

	cmd := exec.CommandContext(ctx, args[0], args[1:]...)
	cmd.Dir = filepath.Join(repoRoot, orDot(spec.Cwd))
	cmd.Env = mergeEnv(spec.Env)
	if spec.Mode == ModeStdin {
		cmd.Stdin = strings.NewReader(prompt) // stdin ONLY in stdin mode (B2)
	}

	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return "", err
	}
	var stderr bytes.Buffer
	cmd.Stderr = &stderr

	if err := cmd.Start(); err != nil {
		return "", fmt.Errorf("failed to start agent: %w", err)
	}

	// Stream stdout to the sink while collecting the full output.
	var collected bytes.Buffer
	tee := io.TeeReader(stdout, &collected)
	streamErr := pump(tee, sink)

	waitErr := cmd.Wait()
	if waitErr != nil {
		if ctx.Err() == context.DeadlineExceeded {
			return collected.String(), fmt.Errorf("agent timed out")
		}
		if msg := strings.TrimSpace(stderr.String()); msg != "" {
			return collected.String(), fmt.Errorf("agent failed: %s", msg)
		}
		return collected.String(), fmt.Errorf("agent failed: %w", waitErr)
	}
	if streamErr != nil {
		return collected.String(), streamErr
	}
	return collected.String(), nil
}

// pump copies src to dst in small chunks so output appears live.
func pump(src io.Reader, dst io.Writer) error {
	r := bufio.NewReader(src)
	buf := make([]byte, 4096)
	for {
		n, err := r.Read(buf)
		if n > 0 {
			if _, werr := dst.Write(buf[:n]); werr != nil {
				return werr
			}
		}
		if err == io.EOF {
			return nil
		}
		if err != nil {
			return err
		}
	}
}

func orDot(s string) string {
	if s == "" {
		return "."
	}
	return s
}

func mergeEnv(extra map[string]string) []string {
	env := os.Environ()
	for k, v := range extra {
		env = append(env, k+"="+v)
	}
	return env
}

// wrapForWindows prepends the appropriate interpreter for .cmd/.bat/.ps1
// targets, mirroring the legacy Windows handling.
func wrapForWindows(args []string) []string {
	if runtime.GOOS != "windows" || len(args) == 0 {
		return args
	}
	switch strings.ToLower(filepath.Ext(args[0])) {
	case ".cmd", ".bat":
		comspec := os.Getenv("ComSpec")
		if comspec == "" {
			comspec = `C:\Windows\System32\cmd.exe`
		}
		return append([]string{comspec, "/d", "/s", "/c"}, args...)
	case ".ps1":
		ps, err := exec.LookPath("powershell")
		if err != nil {
			ps = `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`
		}
		return append([]string{ps, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File"}, args...)
	}
	return args
}
