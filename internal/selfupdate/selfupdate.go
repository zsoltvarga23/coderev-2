// Package selfupdate implements `coderev update`: it checks the project's
// GitHub Releases for a newer CLI build, verifies its SHA-256 against a
// published checksums file, and atomically replaces the running binary.
//
// It is standard-library only (no third-party deps), matching the engine's
// minimal-dependency design. The GUI updates itself separately via Velopack;
// this covers the command-line tool.
package selfupdate

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"time"
)

// DefaultRepo is the GitHub "owner/repo" the updater queries. Overridable via
// the CODEREV_UPDATE_REPO environment variable (e.g. for a fork or mirror).
const DefaultRepo = "zsoltvarga23/coderev-2"

const apiBase = "https://api.github.com"

// Options configures an update run.
type Options struct {
	Repo           string       // "owner/repo"; empty => DefaultRepo (or env)
	CurrentVersion string       // running version, e.g. "1.0.0" or "v1.0.0"
	CheckOnly      bool         // report availability but do not install
	Client         *http.Client // nil => a sane default
	Out            io.Writer    // progress/result messages; nil => os.Stdout
}

type release struct {
	TagName    string  `json:"tag_name"`
	Name       string  `json:"name"`
	HTMLURL    string  `json:"html_url"`
	Prerelease bool    `json:"prerelease"`
	Assets     []asset `json:"assets"`
}

type asset struct {
	Name string `json:"name"`
	URL  string `json:"browser_download_url"`
	Size int64  `json:"size"`
}

// AssetName is the expected CLI binary asset for the current platform, e.g.
// "coderev-windows-amd64.exe" or "coderev-linux-amd64". The release workflow
// publishes assets under these names.
func AssetName() string {
	n := fmt.Sprintf("coderev-%s-%s", runtime.GOOS, runtime.GOARCH)
	if runtime.GOOS == "windows" {
		n += ".exe"
	}
	return n
}

// Run performs the update (or check). It returns nil on success, including the
// "already up to date" case.
func Run(opts Options) error {
	out := opts.Out
	if out == nil {
		out = os.Stdout
	}
	repo := opts.Repo
	if repo == "" {
		if env := os.Getenv("CODEREV_UPDATE_REPO"); env != "" {
			repo = env
		} else {
			repo = DefaultRepo
		}
	}
	client := opts.Client
	if client == nil {
		client = &http.Client{Timeout: 60 * time.Second}
	}

	fmt.Fprintln(out, "Checking for updates…")
	rel, err := latestRelease(client, repo)
	if err != nil {
		return err
	}

	latest := normalizeVersion(rel.TagName)
	current := normalizeVersion(opts.CurrentVersion)
	cmp, err := compareVersions(latest, current)
	if err != nil {
		return fmt.Errorf("comparing versions (%q vs %q): %w", latest, current, err)
	}
	if cmp <= 0 {
		fmt.Fprintf(out, "Already up to date (v%s).\n", current)
		return nil
	}

	fmt.Fprintf(out, "A new version is available: v%s (you have v%s).\n", latest, current)
	if rel.HTMLURL != "" {
		fmt.Fprintf(out, "Release notes: %s\n", rel.HTMLURL)
	}
	if opts.CheckOnly {
		fmt.Fprintln(out, "Run `coderev update` to install it.")
		return nil
	}

	// Locate the platform binary and the checksums file.
	want := AssetName()
	bin := findAsset(rel.Assets, want)
	if bin == nil {
		return fmt.Errorf("release v%s has no asset named %q (platform %s/%s)", latest, want, runtime.GOOS, runtime.GOARCH)
	}
	sums := findAsset(rel.Assets, "checksums.txt")

	fmt.Fprintf(out, "Downloading %s (%s)…\n", bin.Name, humanBytes(bin.Size))
	data, err := download(client, bin.URL)
	if err != nil {
		return fmt.Errorf("downloading %s: %w", bin.Name, err)
	}

	// Verify integrity against the published checksum. We treat a missing
	// checksums file as fatal: an unverifiable binary should never be installed.
	if sums == nil {
		return fmt.Errorf("release v%s has no checksums.txt — refusing to install an unverified binary", latest)
	}
	sumData, err := download(client, sums.URL)
	if err != nil {
		return fmt.Errorf("downloading checksums.txt: %w", err)
	}
	want256, ok := checksumFor(string(sumData), bin.Name)
	if !ok {
		return fmt.Errorf("checksums.txt has no entry for %s", bin.Name)
	}
	got256 := sha256Hex(data)
	if !strings.EqualFold(got256, want256) {
		return fmt.Errorf("checksum mismatch for %s: expected %s, got %s", bin.Name, want256, got256)
	}
	fmt.Fprintln(out, "Checksum verified.")

	if err := replaceExecutable(data); err != nil {
		return fmt.Errorf("installing update: %w", err)
	}

	fmt.Fprintf(out, "Updated to v%s. Restart coderev to use the new version.\n", latest)
	return nil
}

// latestRelease fetches the newest non-draft, non-prerelease release.
func latestRelease(client *http.Client, repo string) (*release, error) {
	url := fmt.Sprintf("%s/repos/%s/releases/latest", apiBase, repo)
	req, err := http.NewRequest(http.MethodGet, url, nil)
	if err != nil {
		return nil, err
	}
	req.Header.Set("Accept", "application/vnd.github+json")
	// Authorize if a token is present (raises rate limits / private repos).
	if tok := os.Getenv("GITHUB_TOKEN"); tok != "" {
		req.Header.Set("Authorization", "Bearer "+tok)
	}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	if resp.StatusCode == http.StatusNotFound {
		return nil, fmt.Errorf("no published release found for %s", repo)
	}
	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(io.LimitReader(resp.Body, 512))
		return nil, fmt.Errorf("GitHub API returned %s: %s", resp.Status, strings.TrimSpace(string(body)))
	}
	var rel release
	if err := json.NewDecoder(resp.Body).Decode(&rel); err != nil {
		return nil, fmt.Errorf("parsing release JSON: %w", err)
	}
	return &rel, nil
}

func download(client *http.Client, url string) ([]byte, error) {
	resp, err := client.Get(url)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("HTTP %s", resp.Status)
	}
	return io.ReadAll(resp.Body)
}

func findAsset(assets []asset, name string) *asset {
	for i := range assets {
		if assets[i].Name == name {
			return &assets[i]
		}
	}
	return nil
}

// replaceExecutable atomically swaps the running binary for newData. It writes
// the new bytes next to the current executable (same volume, so os.Rename is
// atomic), moves the old binary aside, then renames the new one into place.
func replaceExecutable(newData []byte) error {
	exe, err := os.Executable()
	if err != nil {
		return err
	}
	if resolved, err := filepath.EvalSymlinks(exe); err == nil {
		exe = resolved
	}
	dir := filepath.Dir(exe)

	newPath := exe + ".new"
	if err := os.WriteFile(newPath, newData, 0o755); err != nil {
		return fmt.Errorf("writing to %s (is the install directory writable?): %w", dir, err)
	}

	oldPath := exe + ".old"
	_ = os.Remove(oldPath) // clear any stale leftover from a previous update

	// A running executable cannot be deleted on Windows, but it CAN be renamed;
	// move it aside so the new binary can take its place.
	if err := os.Rename(exe, oldPath); err != nil {
		_ = os.Remove(newPath)
		return err
	}
	if err := os.Rename(newPath, exe); err != nil {
		_ = os.Rename(oldPath, exe) // roll back
		return err
	}
	_ = os.Chmod(exe, 0o755)
	// Best-effort cleanup. On Windows the .old file is still locked until this
	// process exits; it's harmless and overwritten on the next update.
	_ = os.Remove(oldPath)
	return nil
}

// ---- version helpers -----------------------------------------------------

func normalizeVersion(v string) string {
	v = strings.TrimSpace(v)
	v = strings.TrimPrefix(v, "v")
	// Drop any pre-release/build suffix for comparison (e.g. "1.2.0-rc1").
	if i := strings.IndexAny(v, "-+"); i >= 0 {
		v = v[:i]
	}
	return v
}

// compareVersions returns -1, 0, or 1 for a<b, a==b, a>b over dotted numeric
// versions ("1.2.0"). Missing components are treated as zero.
func compareVersions(a, b string) (int, error) {
	pa := strings.Split(a, ".")
	pb := strings.Split(b, ".")
	n := len(pa)
	if len(pb) > n {
		n = len(pb)
	}
	for i := 0; i < n; i++ {
		na, err := numAt(pa, i)
		if err != nil {
			return 0, err
		}
		nb, err := numAt(pb, i)
		if err != nil {
			return 0, err
		}
		switch {
		case na < nb:
			return -1, nil
		case na > nb:
			return 1, nil
		}
	}
	return 0, nil
}

func numAt(parts []string, i int) (int, error) {
	if i >= len(parts) || parts[i] == "" {
		return 0, nil
	}
	return strconv.Atoi(parts[i])
}

// checksumFor finds the hex digest for filename in a `sha256sum`-style file
// ("<hex>  <filename>" per line).
func checksumFor(contents, filename string) (string, bool) {
	for _, line := range strings.Split(contents, "\n") {
		line = strings.TrimSpace(line)
		if line == "" {
			continue
		}
		fields := strings.Fields(line)
		if len(fields) < 2 {
			continue
		}
		// The name may be prefixed with '*' (binary mode) or a path.
		name := strings.TrimPrefix(fields[len(fields)-1], "*")
		name = filepath.Base(name)
		if name == filename {
			return fields[0], true
		}
	}
	return "", false
}

func sha256Hex(data []byte) string {
	sum := sha256.Sum256(data)
	return hex.EncodeToString(sum[:])
}

func humanBytes(n int64) string {
	switch {
	case n >= 1<<20:
		return fmt.Sprintf("%.1f MB", float64(n)/(1<<20))
	case n >= 1<<10:
		return fmt.Sprintf("%d KB", n/(1<<10))
	default:
		return fmt.Sprintf("%d B", n)
	}
}
