package config

import (
	"bytes"
	"os"
	"path/filepath"
	"testing"
)

func TestParseInit_Defaults(t *testing.T) {
	repo := t.TempDir()
	res, err := ParseInit(nil, repo, &bytes.Buffer{})
	if err != nil {
		t.Fatal(err)
	}
	if res.Path != filepath.Join(repo, ".coderev.json") {
		t.Errorf("path = %q, want repo/.coderev.json", res.Path)
	}
	if res.Config.Agent != "codex" || res.Config.BaseRef != "origin/main" {
		t.Errorf("init defaults wrong: %+v", res.Config)
	}
}

func TestParseInit_WithOptions(t *testing.T) {
	repo := t.TempDir()
	res, err := ParseInit([]string{"--agent", "copilot", "--base-ref", "origin/develop", "--out", "review.md", "--force"}, repo, &bytes.Buffer{})
	if err != nil {
		t.Fatal(err)
	}
	if !res.Force {
		t.Error("force not set")
	}
	if res.Config.Agent != "copilot" || res.Config.BaseRef != "origin/develop" || res.Config.Out != "review.md" {
		t.Errorf("options not captured: %+v", res.Config)
	}
}

func TestParseInit_CustomPath(t *testing.T) {
	repo := t.TempDir()
	res, err := ParseInit([]string{"custom.json"}, repo, &bytes.Buffer{})
	if err != nil {
		t.Fatal(err)
	}
	if res.Path != filepath.Join(repo, "custom.json") {
		t.Errorf("path = %q, want repo/custom.json", res.Path)
	}
}

func TestParseInit_IgnoresEnvAndFile(t *testing.T) {
	// init must be predictable: an existing config file is NOT read.
	repo := t.TempDir()
	writeConfig(t, repo, `{"agent":"copilot","base-ref":"origin/x"}`)
	res, err := ParseInit(nil, repo, &bytes.Buffer{})
	if err != nil {
		t.Fatal(err)
	}
	if res.Config.Agent != "codex" {
		t.Errorf("init should ignore existing file, got agent=%q", res.Config.Agent)
	}
}

func TestGenerateJSON_RoundTrip(t *testing.T) {
	repo := t.TempDir()
	res, err := ParseInit([]string{"--agent", "copilot", "--context-lines", "30", "--obey-doc", "STYLE.md"}, repo, &bytes.Buffer{})
	if err != nil {
		t.Fatal(err)
	}
	data, err := GenerateJSON(res.Config)
	if err != nil {
		t.Fatal(err)
	}
	// Write it, then load it back via the normal Parse path and verify values.
	p := filepath.Join(repo, ".coderev.json")
	if err := os.WriteFile(p, data, 0o644); err != nil {
		t.Fatal(err)
	}
	parsed := parseOK(t, []string{"br"}, nil, repo)
	if parsed.Agent != "copilot" {
		t.Errorf("round-trip agent = %q, want copilot", parsed.Agent)
	}
	if parsed.ContextLines != 30 {
		t.Errorf("round-trip context-lines = %d, want 30", parsed.ContextLines)
	}
	if len(parsed.ObeyDoc) != 1 || parsed.ObeyDoc[0] != "STYLE.md" {
		t.Errorf("round-trip obey-doc = %v, want [STYLE.md]", parsed.ObeyDoc)
	}
}

func TestWriteConfigFile_NoClobber(t *testing.T) {
	repo := t.TempDir()
	p := filepath.Join(repo, ".coderev.json")
	if err := os.WriteFile(p, []byte("{}"), 0o644); err != nil {
		t.Fatal(err)
	}
	if err := WriteConfigFile(p, []byte("{}"), false); err == nil {
		t.Error("expected error when file exists and force=false")
	}
	if err := WriteConfigFile(p, []byte(`{"agent":"x"}`), true); err != nil {
		t.Errorf("force write failed: %v", err)
	}
}
