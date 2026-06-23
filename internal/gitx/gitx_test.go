package gitx

import (
	"context"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
)

// setupRepo builds a throwaway git repo with a main branch and a feature
// branch that adds and modifies files, returning the repo handle.
func setupRepo(t *testing.T) *Repo {
	t.Helper()
	if _, err := exec.LookPath("git"); err != nil {
		t.Skip("git not available")
	}
	dir := t.TempDir()
	ctx := context.Background()

	gitCmd := func(args ...string) {
		cmd := exec.CommandContext(ctx, "git", args...)
		cmd.Dir = dir
		cmd.Env = append(os.Environ(),
			"GIT_AUTHOR_NAME=t", "GIT_AUTHOR_EMAIL=t@t",
			"GIT_COMMITTER_NAME=t", "GIT_COMMITTER_EMAIL=t@t",
		)
		if out, err := cmd.CombinedOutput(); err != nil {
			t.Fatalf("git %s: %v\n%s", strings.Join(args, " "), err, out)
		}
	}
	write := func(name, content string) {
		if err := os.WriteFile(filepath.Join(dir, name), []byte(content), 0o644); err != nil {
			t.Fatal(err)
		}
	}

	gitCmd("init", "-b", "main")
	write("a.txt", "line1\nline2\nline3\n")
	gitCmd("add", ".")
	gitCmd("commit", "-m", "init")

	gitCmd("checkout", "-b", "feature")
	write("a.txt", "line1\nCHANGED\nline3\n")
	write("b.txt", "brand new\n")
	gitCmd("add", ".")
	gitCmd("commit", "-m", "feature work")
	// Leave the repo checked out on feature; gitx must not depend on branch.

	return &Repo{Root: dir}
}

func TestChangedFiles(t *testing.T) {
	r := setupRepo(t)
	files, err := r.ChangedFiles(context.Background(), "main", "feature")
	if err != nil {
		t.Fatal(err)
	}
	got := strings.Join(files, ",")
	if got != "a.txt,b.txt" {
		t.Errorf("changed files = %q, want a.txt,b.txt", got)
	}
}

func TestDiff(t *testing.T) {
	r := setupRepo(t)
	diff, err := r.Diff(context.Background(), "main", "feature")
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(diff, "+CHANGED") {
		t.Errorf("diff missing +CHANGED:\n%s", diff)
	}
	if !strings.Contains(diff, "b.txt") {
		t.Errorf("diff missing b.txt:\n%s", diff)
	}
}

func TestFileAtRef(t *testing.T) {
	r := setupRepo(t)
	ctx := context.Background()

	got, ok := r.FileAtRef(ctx, "feature", "a.txt")
	if !ok || !strings.Contains(got, "CHANGED") {
		t.Errorf("feature a.txt = %q (ok=%v), want CHANGED", got, ok)
	}
	old, ok := r.FileAtRef(ctx, "main", "a.txt")
	if !ok || strings.Contains(old, "CHANGED") {
		t.Errorf("main a.txt should be the original, got %q", old)
	}
	if _, ok := r.FileAtRef(ctx, "main", "b.txt"); ok {
		t.Error("b.txt should not exist at main")
	}
}

func TestMergeBase(t *testing.T) {
	r := setupRepo(t)
	mb := r.MergeBase(context.Background(), "main", "feature")
	if len(mb) < 7 {
		t.Errorf("merge-base = %q, want a commit hash", mb)
	}
}

func TestVerifyRef(t *testing.T) {
	r := setupRepo(t)
	ctx := context.Background()
	if err := r.VerifyRef(ctx, "feature"); err != nil {
		t.Errorf("VerifyRef(feature) = %v, want nil", err)
	}
	if err := r.VerifyRef(ctx, "nope"); err == nil {
		t.Error("VerifyRef(nope) = nil, want error")
	}
}
