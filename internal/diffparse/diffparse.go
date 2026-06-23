// Package diffparse extracts changed hunks from a unified diff and builds
// human-readable context snippets around each change.
package diffparse

import (
	"fmt"
	"regexp"
	"strconv"
	"strings"
)

// Hunk describes one changed region of one file.
type Hunk struct {
	FilePath string
	StartNew int // first line number in the new file
	CountNew int
	StartOld int
	CountOld int
}

var hunkRE = regexp.MustCompile(`^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@`)

// ParseHunks extracts hunks from a unified diff. It tracks the current file via
// the "+++ b/<path>" header and ignores deletions ("+++ /dev/null"), so deleted
// files do not produce bogus context requests (legacy gap K5).
func ParseHunks(diff string) []Hunk {
	var hunks []Hunk
	current := ""
	for _, line := range strings.Split(diff, "\n") {
		switch {
		case strings.HasPrefix(line, "diff --git "):
			current = ""
		case strings.HasPrefix(line, "+++ "):
			target := strings.TrimSpace(line[len("+++ "):])
			if target == "/dev/null" {
				current = "" // file deleted at head; no new-side content
				continue
			}
			current = strings.TrimPrefix(target, "b/")
		default:
			if current == "" {
				continue
			}
			m := hunkRE.FindStringSubmatch(line)
			if m == nil {
				continue
			}
			hunks = append(hunks, Hunk{
				FilePath: current,
				StartOld: atoi(m[1]),
				CountOld: atoiOr(m[2], 1),
				StartNew: atoi(m[3]),
				CountNew: atoiOr(m[4], 1),
			})
		}
	}
	return hunks
}

// FileSource provides file content at the reviewed ref. Returning ok=false
// means the file is unavailable (binary, deleted) and is skipped.
type FileSource func(path string) (content string, ok bool)

// BuildSnippets groups hunks by file and renders a context snippet per file,
// reading each file from src (the head ref) rather than the working tree
// (legacy bug B3). contextLines lines surround each hunk; changed lines are
// marked ">>". Each file's snippet is truncated to maxChars on a line boundary.
func BuildSnippets(hunks []Hunk, src FileSource, contextLines, maxChars int) map[string]string {
	grouped := map[string][]Hunk{}
	var order []string
	for _, h := range hunks {
		if _, seen := grouped[h.FilePath]; !seen {
			order = append(order, h.FilePath)
		}
		grouped[h.FilePath] = append(grouped[h.FilePath], h)
	}

	out := map[string]string{}
	for _, path := range order {
		content, ok := src(path)
		if !ok {
			continue
		}
		lines := strings.Split(strings.ReplaceAll(content, "\r\n", "\n"), "\n")
		var parts []string
		for _, h := range grouped[path] {
			cnt := h.CountNew
			if cnt < 1 {
				cnt = 1
			}
			start := h.StartNew - contextLines
			if start < 1 {
				start = 1
			}
			end := h.StartNew + cnt + contextLines - 1
			if end > len(lines) {
				end = len(lines)
			}
			var block []string
			for i := start; i <= end; i++ {
				prefix := "  "
				if i >= h.StartNew && i < h.StartNew+cnt {
					prefix = ">>"
				}
				block = append(block, fmt.Sprintf("%s %6d | %s", prefix, i, lines[i-1]))
			}
			parts = append(parts, fmt.Sprintf(
				"--- Context around hunk (+%d,%d) in %s ---\n%s",
				h.StartNew, h.CountNew, path, strings.Join(block, "\n")))
		}
		combined := strings.TrimSpace(strings.Join(parts, "\n\n"))
		out[path] = truncateLines(combined, maxChars)
	}
	return out
}

// truncateLines trims s to at most maxChars, cutting on a line boundary so a
// snippet never ends mid-line.
func truncateLines(s string, maxChars int) string {
	if maxChars <= 0 || len(s) <= maxChars {
		return s
	}
	cut := s[:maxChars]
	if i := strings.LastIndex(cut, "\n"); i > 0 {
		cut = cut[:i]
	}
	return cut + "\n... (snippet truncated) ..."
}

func atoi(s string) int {
	n, _ := strconv.Atoi(s)
	return n
}

func atoiOr(s string, def int) int {
	if s == "" {
		return def
	}
	return atoi(s)
}
