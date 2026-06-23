package prompt

import (
	"strings"
	"testing"
)

func baseInput() Input {
	return Input{
		Lang:         "hu",
		Branch:       "feature/x",
		BaseRef:      "origin/main",
		ChangedFiles: []string{"a.txt", "b.txt"},
		Diff:         "@@ -1 +1 @@\n-old\n+new",
	}
}

func TestBuild_Hungarian(t *testing.T) {
	out := Build(baseInput())
	for _, want := range []string{"# Feladat: Automatikus PR review", "Vizsgált ág: feature/x", "## Módosult fájlok", "- a.txt", "## Diff", "+new"} {
		if !strings.Contains(out, want) {
			t.Errorf("prompt missing %q", want)
		}
	}
}

func TestBuild_English(t *testing.T) {
	in := baseInput()
	in.Lang = "en"
	out := Build(in)
	if !strings.Contains(out, "# Task: Automated PR Review") {
		t.Errorf("english prompt missing task heading:\n%s", out)
	}
	if strings.Contains(out, "Feladat") {
		t.Error("english prompt should not contain Hungarian headings")
	}
}

func TestBuild_NoDiff(t *testing.T) {
	in := baseInput()
	in.Diff = "   "
	out := Build(in)
	if !strings.Contains(out, "(Nincs diff)") {
		t.Errorf("expected no-diff marker:\n%s", out)
	}
}

func TestBuild_SectionsOptional(t *testing.T) {
	out := Build(baseInput())
	if strings.Contains(out, "Betartandó") {
		t.Error("obey section should be absent when no docs")
	}
	if strings.Contains(out, "teljes tartalma") {
		t.Error("full-files section should be absent")
	}
}

func TestBuild_WithDocsAndTemplate(t *testing.T) {
	in := baseInput()
	in.ObeyDocs = []Doc{{Name: "STYLE.md", Content: "be nice"}}
	in.Template = &Doc{Name: "tpl.md", Content: "## Result"}
	in.Snippets = map[string]string{"a.txt": ">> 1 | new"}
	out := Build(in)
	for _, want := range []string{"### STYLE.md", "be nice", "### tpl.md", "Extra kontextus", ">> 1 | new"} {
		if !strings.Contains(out, want) {
			t.Errorf("prompt missing %q", want)
		}
	}
}

func TestTruncateBytes_NoSplit(t *testing.T) {
	// "é" is two bytes in UTF-8; force a cut near a boundary.
	s := strings.Repeat("é", 100)
	out := TruncateBytes(s, 51)
	if !strings.Contains(out, "truncated") {
		t.Fatalf("expected truncation marker")
	}
	// The visible parts must remain valid UTF-8 (no replacement chars).
	if strings.ContainsRune(out, '�') {
		t.Error("truncation split a multi-byte rune")
	}
}

func TestTruncateBytes_Short(t *testing.T) {
	if got := TruncateBytes("hello", 0); got != "hello" {
		t.Errorf("no-limit should return input, got %q", got)
	}
	if got := TruncateBytes("hello", 100); got != "hello" {
		t.Errorf("under-limit should return input, got %q", got)
	}
}
