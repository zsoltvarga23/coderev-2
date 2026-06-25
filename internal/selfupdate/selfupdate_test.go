package selfupdate

import (
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
