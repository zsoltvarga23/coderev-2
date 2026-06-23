package diffparse

import (
	"strings"
	"testing"
)

const sampleDiff = `diff --git a/a.txt b/a.txt
index 111..222 100644
--- a/a.txt
+++ b/a.txt
@@ -1,3 +1,3 @@
 line1
-line2
+CHANGED
 line3
diff --git a/b.txt b/b.txt
new file mode 100644
index 000..333
--- /dev/null
+++ b/b.txt
@@ -0,0 +1 @@
+brand new
diff --git a/gone.txt b/gone.txt
deleted file mode 100644
index 444..000
--- a/gone.txt
+++ /dev/null
@@ -1 +0,0 @@
-was here
`

func TestParseHunks(t *testing.T) {
	hunks := ParseHunks(sampleDiff)
	if len(hunks) != 2 {
		t.Fatalf("got %d hunks, want 2 (deleted file excluded)", len(hunks))
	}
	if hunks[0].FilePath != "a.txt" || hunks[0].StartNew != 1 || hunks[0].CountNew != 3 {
		t.Errorf("hunk0 = %+v", hunks[0])
	}
	if hunks[1].FilePath != "b.txt" || hunks[1].StartNew != 1 || hunks[1].CountNew != 1 {
		t.Errorf("hunk1 = %+v", hunks[1])
	}
	for _, h := range hunks {
		if h.FilePath == "gone.txt" {
			t.Error("deleted file gone.txt should not produce a hunk")
		}
	}
}

func TestParseHunks_DefaultCount(t *testing.T) {
	// "@@ -5 +7 @@" omits counts, which default to 1.
	d := "+++ b/x.go\n@@ -5 +7 @@\n+added\n"
	hunks := ParseHunks(d)
	if len(hunks) != 1 || hunks[0].StartNew != 7 || hunks[0].CountNew != 1 {
		t.Fatalf("hunks = %+v", hunks)
	}
}

func TestBuildSnippets(t *testing.T) {
	hunks := ParseHunks(sampleDiff)
	src := func(path string) (string, bool) {
		switch path {
		case "a.txt":
			return "line1\nCHANGED\nline3\n", true
		case "b.txt":
			return "brand new\n", true
		}
		return "", false
	}
	snips := BuildSnippets(hunks, src, 2, 25000)
	a := snips["a.txt"]
	if !strings.Contains(a, ">>      2 | CHANGED") {
		t.Errorf("a.txt snippet missing marked change:\n%s", a)
	}
	if !strings.Contains(a, "around hunk (+1,3) in a.txt") {
		t.Errorf("a.txt snippet missing header:\n%s", a)
	}
	if _, ok := snips["b.txt"]; !ok {
		t.Error("b.txt snippet missing")
	}
}

func TestBuildSnippets_SkipsUnavailable(t *testing.T) {
	hunks := ParseHunks(sampleDiff)
	src := func(path string) (string, bool) { return "", false } // e.g. binary
	snips := BuildSnippets(hunks, src, 2, 25000)
	if len(snips) != 0 {
		t.Errorf("expected no snippets when source unavailable, got %d", len(snips))
	}
}

func TestTruncateLines(t *testing.T) {
	s := "aaaa\nbbbb\ncccc\ndddd"
	out := truncateLines(s, 7)
	if !strings.Contains(out, "snippet truncated") {
		t.Errorf("expected truncation marker, got %q", out)
	}
	if strings.Contains(out, "cccc") {
		t.Errorf("truncation should cut on line boundary, got %q", out)
	}
}
