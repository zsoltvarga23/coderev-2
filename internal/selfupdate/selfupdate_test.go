package selfupdate

import (
	"bytes"
	"fmt"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func TestNormalizeVersion(t *testing.T) {
	cases := map[string]string{
		"v1.2.0":       "1.2.0",
		"1.2.0":        "1.2.0",
		" v1.0.0 ":     "1.0.0",
		"1.2.0-rc1":    "1.2.0",
		"v2.0.0+build": "2.0.0",
	}
	for in, want := range cases {
		if got := normalizeVersion(in); got != want {
			t.Errorf("normalizeVersion(%q) = %q, want %q", in, got, want)
		}
	}
}

func TestCompareVersions(t *testing.T) {
	cases := []struct {
		a, b string
		want int
	}{
		{"1.2.0", "1.1.0", 1},
		{"1.1.0", "1.2.0", -1},
		{"1.2.0", "1.2.0", 0},
		{"1.2.1", "1.2.0", 1},
		{"2.0.0", "1.9.9", 1},
		{"1.2", "1.2.0", 0},    // missing component treated as zero
		{"1.10.0", "1.9.0", 1}, // numeric, not lexical
	}
	for _, c := range cases {
		got, err := compareVersions(c.a, c.b)
		if err != nil {
			t.Fatalf("compareVersions(%q,%q) error: %v", c.a, c.b, err)
		}
		if got != c.want {
			t.Errorf("compareVersions(%q,%q) = %d, want %d", c.a, c.b, got, c.want)
		}
	}
}

func TestAssetName(t *testing.T) {
	got := AssetName()
	if !strings.HasPrefix(got, "coderev-"+runtime.GOOS+"-"+runtime.GOARCH) {
		t.Errorf("AssetName() = %q, missing platform suffix", got)
	}
	if runtime.GOOS == "windows" && !strings.HasSuffix(got, ".exe") {
		t.Errorf("AssetName() = %q, want .exe suffix on windows", got)
	}
}

func TestChecksumFor(t *testing.T) {
	contents := strings.Join([]string{
		"abc123  coderev-linux-amd64",
		"def456 *coderev-windows-amd64.exe",
		"# a comment line",
		"ghi789  some/path/coderev-darwin-arm64",
	}, "\n")

	if sum, ok := checksumFor(contents, "coderev-linux-amd64"); !ok || sum != "abc123" {
		t.Errorf("linux: got (%q,%v), want (abc123,true)", sum, ok)
	}
	// '*' binary-mode prefix must be stripped.
	if sum, ok := checksumFor(contents, "coderev-windows-amd64.exe"); !ok || sum != "def456" {
		t.Errorf("windows: got (%q,%v), want (def456,true)", sum, ok)
	}
	// Path prefix reduced to base name.
	if sum, ok := checksumFor(contents, "coderev-darwin-arm64"); !ok || sum != "ghi789" {
		t.Errorf("darwin: got (%q,%v), want (ghi789,true)", sum, ok)
	}
	if _, ok := checksumFor(contents, "missing"); ok {
		t.Error("missing: expected not found")
	}
}

func TestChecksumForSkipsComments(t *testing.T) {
	// A comment whose last word collides with a real asset name must not be
	// mistaken for a checksum entry.
	contents := "# coderev-linux-amd64\nabc123  coderev-linux-amd64\n"
	if sum, ok := checksumFor(contents, "coderev-linux-amd64"); !ok || sum != "abc123" {
		t.Errorf("got (%q,%v), want (abc123,true)", sum, ok)
	}
}

func TestReplaceFile(t *testing.T) {
	dir := t.TempDir()
	target := filepath.Join(dir, "coderev.bin")
	if err := os.WriteFile(target, []byte("OLD"), 0o755); err != nil {
		t.Fatal(err)
	}
	if err := replaceFile(target, []byte("NEW-CONTENT")); err != nil {
		t.Fatalf("replaceFile: %v", err)
	}
	got, err := os.ReadFile(target)
	if err != nil {
		t.Fatal(err)
	}
	if string(got) != "NEW-CONTENT" {
		t.Errorf("content = %q, want NEW-CONTENT", got)
	}
	if _, err := os.Stat(target + ".new"); !os.IsNotExist(err) {
		t.Errorf(".new sibling should not remain after a successful swap")
	}
}

// TestRun exercises the network-driven paths that do NOT install (so the test
// binary is never touched) via a mock GitHub API.
func TestRun(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/releases/latest") {
			w.Header().Set("Content-Type", "application/json")
			fmt.Fprintf(w, `{"tag_name":"v2.0.0","html_url":"http://example/notes","assets":[]}`)
			return
		}
		http.NotFound(w, r)
	}))
	defer srv.Close()

	orig := apiBase
	apiBase = srv.URL
	defer func() { apiBase = orig }()

	t.Run("check-only reports a newer version", func(t *testing.T) {
		var buf bytes.Buffer
		err := Run(Options{Repo: "owner/repo", CurrentVersion: "1.0.0", CheckOnly: true, Client: srv.Client(), Out: &buf})
		if err != nil {
			t.Fatalf("Run: %v", err)
		}
		if !strings.Contains(buf.String(), "new version is available") {
			t.Errorf("missing new-version notice:\n%s", buf.String())
		}
	})

	t.Run("already up to date", func(t *testing.T) {
		var buf bytes.Buffer
		err := Run(Options{Repo: "owner/repo", CurrentVersion: "2.0.0", Client: srv.Client(), Out: &buf})
		if err != nil {
			t.Fatalf("Run: %v", err)
		}
		if !strings.Contains(buf.String(), "Already up to date") {
			t.Errorf("missing up-to-date notice:\n%s", buf.String())
		}
	})
}
