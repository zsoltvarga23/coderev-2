// Package prompt assembles the review prompt sent to the AI agent. Section
// headings are localized (hu/en); the instructions ask the model to answer in
// the chosen language.
package prompt

import (
	"strings"
	"unicode/utf8"
)

// Doc is a named text block (an obey-doc, a template, or a full file).
type Doc struct {
	Name    string
	Content string
}

// Input is everything needed to build a prompt.
type Input struct {
	Lang         string // "hu" | "en"
	Branch       string
	BaseRef      string
	ChangedFiles []string
	Diff         string
	ObeyDocs     []Doc
	Template     *Doc
	Snippets     map[string]string // keyed by file path; emitted in ChangedFiles order
	FullFiles    []Doc             // populated only when --include-full-files
}

type labels struct {
	task, branch, base, instr, instrBody  string
	obey, template, changed, diff, noDiff string
	extra, fullFiles, output, outputBody  string
}

func labelsFor(lang string) labels {
	if lang == "en" {
		return labels{
			task:   "# Task: Automated PR Review",
			branch: "Branch under review",
			base:   "Diff base",
			instr:  "## Instructions",
			instrBody: "Perform a PR review. Obey the provided documentation. " +
				"Focus on correctness, maintainability, security, performance, and tests. " +
				"Write your review in English.",
			obey:      "## Documentation to OBEY (highest priority)",
			template:  "## Result Template (fill this in)",
			changed:   "## Changed Files",
			diff:      "## Diff",
			noDiff:    "(No diff)",
			extra:     "## Extra Context (snippets around changes)",
			fullFiles: "## Full Contents of Changed Files",
			output:    "## Output Requirements",
			outputBody: "- If a result template was provided, fill it in.\n" +
				"- Otherwise, output markdown with: Summary, Major Issues, Minor Issues, Tests, Suggestions.\n" +
				"- Be specific (file paths and line ranges when possible).",
		}
	}
	return labels{
		task:   "# Feladat: Automatikus PR review",
		branch: "Vizsgált ág",
		base:   "Diff bázis",
		instr:  "## Utasítások",
		instrBody: "Végezz PR review-t. Tartsd be a megadott dokumentációt. " +
			"Koncentrálj a helyességre, karbantarthatóságra, biztonságra, teljesítményre és a tesztekre. " +
			"A review-t magyarul írd.",
		obey:      "## Betartandó dokumentáció (legmagasabb prioritás)",
		template:  "## Eredmény-sablon (ezt töltsd ki)",
		changed:   "## Módosult fájlok",
		diff:      "## Diff",
		noDiff:    "(Nincs diff)",
		extra:     "## Extra kontextus (részletek a változások körül)",
		fullFiles: "## A módosult fájlok teljes tartalma",
		output:    "## Kimeneti elvárások",
		outputBody: "- Ha van eredmény-sablon, töltsd ki azt.\n" +
			"- Egyébként markdown kimenet: Összegzés, Fő problémák, Apró problémák, Tesztek, Javaslatok.\n" +
			"- Légy konkrét (fájlútvonalak és sortartományok, ahol lehet).",
	}
}

// Build assembles the full prompt text.
func Build(in Input) string {
	l := labelsFor(in.Lang)
	var b strings.Builder

	b.WriteString(l.task + "\n\n")
	b.WriteString(l.branch + ": " + in.Branch + "\n")
	b.WriteString(l.base + ": " + in.BaseRef + "\n\n")

	b.WriteString(l.instr + "\n")
	b.WriteString(l.instrBody + "\n")

	if len(in.ObeyDocs) > 0 {
		b.WriteString("\n" + l.obey + "\n")
		for _, d := range in.ObeyDocs {
			b.WriteString("\n### " + d.Name + "\n" + d.Content + "\n")
		}
	}

	if in.Template != nil {
		b.WriteString("\n" + l.template + "\n")
		b.WriteString("\n### " + in.Template.Name + "\n" + in.Template.Content + "\n")
	}

	b.WriteString("\n" + l.changed + "\n")
	for _, f := range in.ChangedFiles {
		b.WriteString("- " + f + "\n")
	}

	b.WriteString("\n" + l.diff + "\n")
	if strings.TrimSpace(in.Diff) == "" {
		b.WriteString(l.noDiff + "\n")
	} else {
		b.WriteString(in.Diff)
		if !strings.HasSuffix(in.Diff, "\n") {
			b.WriteString("\n")
		}
	}

	if len(in.Snippets) > 0 {
		b.WriteString("\n" + l.extra + "\n")
		for _, f := range in.ChangedFiles {
			if snip, ok := in.Snippets[f]; ok && strings.TrimSpace(snip) != "" {
				b.WriteString("\n### " + f + "\n" + snip + "\n")
			}
		}
	}

	if len(in.FullFiles) > 0 {
		b.WriteString("\n" + l.fullFiles + "\n")
		for _, d := range in.FullFiles {
			b.WriteString("\n### " + d.Name + "\n" + d.Content + "\n")
		}
	}

	b.WriteString("\n" + l.output + "\n" + l.outputBody + "\n")

	// Normalize non-breaking hyphen, mirroring the legacy cleanup.
	return strings.ReplaceAll(strings.TrimSpace(b.String()), "‑", "-") + "\n"
}

// TruncateBytes limits s to at most maxBytes, keeping a head and tail and
// cutting on rune boundaries so multi-byte characters are never split
// (legacy bug R6). maxBytes <= 0 means no limit.
func TruncateBytes(s string, maxBytes int) string {
	if maxBytes <= 0 || len(s) <= maxBytes {
		return s
	}
	half := maxBytes / 2
	head := safeCut(s, half, true)
	tail := safeCut(s, half, false)
	return head + "\n\n... (truncated) ...\n\n" + tail
}

// safeCut returns up to n bytes from the front (fromFront) or back of s,
// trimmed so it does not split a UTF-8 rune.
func safeCut(s string, n int, fromFront bool) string {
	if n >= len(s) {
		return s
	}
	if fromFront {
		cut := s[:n]
		for len(cut) > 0 && !utf8.ValidString(cut) {
			cut = cut[:len(cut)-1]
		}
		return cut
	}
	cut := s[len(s)-n:]
	for len(cut) > 0 && !utf8.ValidString(cut) {
		cut = cut[1:]
	}
	return cut
}
