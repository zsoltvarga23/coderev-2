// Package gitx wraps the git CLI. Unlike the legacy tool it never checks out a
// branch or otherwise mutates the user's working tree: diffs and file contents
// are read purely from refs via `git diff` and `git show`.
package gitx

import (
	"bytes"
	"context"
	"fmt"
	"os/exec"
	"strings"
)

// Repo is a handle to a git repository rooted at Root.
type Repo struct {
	Root string
}

// run executes a git command in the repo and returns its stdout. On failure it
// returns an error that includes stderr.
func (r *Repo) run(ctx context.Context, args ...string) (string, error) {
	cmd := exec.CommandContext(ctx, "git", args...)
	if r != nil && r.Root != "" {
		cmd.Dir = r.Root
	}
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr
	if err := cmd.Run(); err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		return "", fmt.Errorf("git %s: %s", strings.Join(args, " "), msg)
	}
	return stdout.String(), nil
}

// Open locates the repository root containing the current directory.
func Open(ctx context.Context) (*Repo, error) {
	cmd := exec.CommandContext(ctx, "git", "rev-parse", "--show-toplevel")
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr
	if err := cmd.Run(); err != nil {
		return nil, fmt.Errorf("not inside a git repository: %s", strings.TrimSpace(stderr.String()))
	}
	return &Repo{Root: strings.TrimSpace(stdout.String())}, nil
}

// Fetch updates the remote implied by baseRef (origin/... or upstream/...),
// defaulting to origin. A returned error is advisory: the caller decides
// whether to treat it as fatal (strict mode) or a warning.
func (r *Repo) Fetch(ctx context.Context, baseRef string) error {
	remote := "origin"
	if i := strings.Index(baseRef, "/"); i > 0 {
		switch baseRef[:i] {
		case "origin", "upstream":
			remote = baseRef[:i]
		}
	}
	_, err := r.run(ctx, "fetch", "--prune", remote)
	return err
}

// VerifyRef returns nil if ref resolves to a commit.
func (r *Repo) VerifyRef(ctx context.Context, ref string) error {
	_, err := r.run(ctx, "rev-parse", "--verify", "--quiet", ref+"^{commit}")
	if err != nil {
		return fmt.Errorf("ref not found: %s", ref)
	}
	return nil
}

// MergeBase returns the merge base of base and head. If it cannot be computed
// (e.g. unrelated histories), it returns base unchanged so the caller can still
// produce a two-dot comparison.
func (r *Repo) MergeBase(ctx context.Context, base, head string) string {
	out, err := r.run(ctx, "merge-base", base, head)
	if err != nil {
		return base
	}
	return strings.TrimSpace(out)
}

// Diff returns the unified diff between base and head using the three-dot
// (merge-base) form, matching the legacy semantics.
func (r *Repo) Diff(ctx context.Context, base, head string) (string, error) {
	return r.run(ctx, "diff", "--no-color", fmt.Sprintf("%s...%s", base, head))
}

// ChangedFiles lists the files changed between base and head (merge-base form).
func (r *Repo) ChangedFiles(ctx context.Context, base, head string) ([]string, error) {
	out, err := r.run(ctx, "diff", "--name-only", fmt.Sprintf("%s...%s", base, head))
	if err != nil {
		return nil, err
	}
	var files []string
	for _, line := range strings.Split(out, "\n") {
		if s := strings.TrimSpace(line); s != "" {
			files = append(files, s)
		}
	}
	return files, nil
}

// FileAtRef returns the content of path as it exists at ref. This is the
// reference-correct alternative to reading the working tree (legacy bug B3).
// A missing path (e.g. deleted at head) yields an empty string and no error.
func (r *Repo) FileAtRef(ctx context.Context, ref, path string) (string, bool) {
	out, err := r.run(ctx, "show", fmt.Sprintf("%s:%s", ref, path))
	if err != nil {
		return "", false
	}
	return out, true
}
