package config

import (
	"bytes"
	"os"
	"path/filepath"
	"testing"
)

func envFrom(m map[string]string) func(string) string {
	return func(k string) string { return m[k] }
}

func parseOK(t *testing.T, args []string, env map[string]string, repo string) Config {
	t.Helper()
	res, err := Parse(args, envFrom(env), repo, &bytes.Buffer{})
	if err != nil {
		t.Fatalf("Parse error: %v", err)
	}
	return res.Config
}

func TestDefaults(t *testing.T) {
	c := parseOK(t, []string{"feature/x"}, nil, "")
	if c.Branch != "feature/x" {
		t.Errorf("branch = %q, want feature/x", c.Branch)
	}
	if c.BaseRef != "origin/main" {
		t.Errorf("base-ref = %q, want origin/main", c.BaseRef)
	}
	if c.ContextLines != 20 {
		t.Errorf("context-lines = %d, want 20", c.ContextLines)
	}
	if c.Lang != "hu" {
		t.Errorf("lang = %q, want hu", c.Lang)
	}
}

func TestMissingBranch(t *testing.T) {
	if _, err := Parse(nil, envFrom(nil), "", &bytes.Buffer{}); err == nil {
		t.Fatal("expected error for missing branch")
	}
}

func TestFlagsAfterPositional(t *testing.T) {
	// Flags may follow the branch argument (coderev <branch> --opt ...).
	c := parseOK(t, []string{"feature/x", "--base-ref", "main", "--lang", "en", "--dry-run"}, nil, "")
	if c.Branch != "feature/x" {
		t.Errorf("branch = %q, want feature/x", c.Branch)
	}
	if c.BaseRef != "main" {
		t.Errorf("base-ref = %q, want main (flag after positional)", c.BaseRef)
	}
	if c.Lang != "en" {
		t.Errorf("lang = %q, want en", c.Lang)
	}
	if !c.DryRun {
		t.Error("dry-run not set when given after positional")
	}
}

func TestPrecedence_CLIBeatsEnv(t *testing.T) {
	env := map[string]string{"CODEREV_BASE_REF": "origin/env"}
	c := parseOK(t, []string{"--base-ref", "origin/cli", "br"}, env, "")
	if c.BaseRef != "origin/cli" {
		t.Errorf("base-ref = %q, want origin/cli (CLI > env)", c.BaseRef)
	}
}

func TestPrecedence_EnvBeatsFile(t *testing.T) {
	repo := t.TempDir()
	writeConfig(t, repo, `{"base-ref":"origin/file","context-lines":5}`)
	env := map[string]string{"CODEREV_BASE_REF": "origin/env"}
	c := parseOK(t, []string{"br"}, env, repo)
	if c.BaseRef != "origin/env" {
		t.Errorf("base-ref = %q, want origin/env (env > file)", c.BaseRef)
	}
	if c.ContextLines != 5 {
		t.Errorf("context-lines = %d, want 5 (from file)", c.ContextLines)
	}
}

func TestPrecedence_FileBeatsDefault(t *testing.T) {
	repo := t.TempDir()
	writeConfig(t, repo, `{"agent":"copilot","include-full-files":true}`)
	c := parseOK(t, []string{"br"}, nil, repo)
	if c.Agent != "copilot" {
		t.Errorf("agent = %q, want copilot", c.Agent)
	}
	if !c.IncludeFullFiles {
		t.Error("include-full-files = false, want true (from file)")
	}
}

func TestEnvBool(t *testing.T) {
	env := map[string]string{"CODEREV_STRICT_FETCH": "yes"}
	c := parseOK(t, []string{"br"}, env, "")
	if !c.StrictFetch {
		t.Error("strict-fetch = false, want true (env yes)")
	}
}

func TestObeyDocStringAndArray(t *testing.T) {
	repo := t.TempDir()
	writeConfig(t, repo, `{"obey-doc":"A.md"}`)
	c := parseOK(t, []string{"--obey-doc", "B.md", "--obey-doc", "C.md", "br"}, nil, repo)
	want := []string{"A.md", "B.md", "C.md"} // file first, then CLI appended
	if len(c.ObeyDoc) != len(want) {
		t.Fatalf("obey-doc = %v, want %v", c.ObeyDoc, want)
	}
	for i := range want {
		if c.ObeyDoc[i] != want[i] {
			t.Errorf("obey-doc[%d] = %q, want %q", i, c.ObeyDoc[i], want[i])
		}
	}
}

func TestNoConfig(t *testing.T) {
	repo := t.TempDir()
	writeConfig(t, repo, `{"base-ref":"origin/file"}`)
	c := parseOK(t, []string{"--no-config", "br"}, nil, repo)
	if c.BaseRef != "origin/main" {
		t.Errorf("base-ref = %q, want origin/main (--no-config)", c.BaseRef)
	}
}

func TestAgentConfigObject(t *testing.T) {
	repo := t.TempDir()
	writeConfig(t, repo, `{"agent-config":{"name":"x","cmd":["x"]}}`)
	c := parseOK(t, []string{"br"}, nil, repo)
	if c.AgentConfig == "" {
		t.Fatal("agent-config empty, want serialized JSON object")
	}
}

func TestInvalidLang(t *testing.T) {
	if _, err := Parse([]string{"--lang", "de", "br"}, envFrom(nil), "", &bytes.Buffer{}); err == nil {
		t.Fatal("expected error for invalid lang")
	}
}

func writeConfig(t *testing.T, repo, content string) {
	t.Helper()
	p := filepath.Join(repo, ".coderev.json")
	if err := os.WriteFile(p, []byte(content), 0o644); err != nil {
		t.Fatal(err)
	}
}
