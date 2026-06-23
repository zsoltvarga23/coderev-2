// Package config resolves coderev settings from four sources, in order of
// decreasing precedence: command-line flags, environment variables, a JSON
// config file, and built-in defaults.
package config

import (
	"encoding/json"
	"strconv"
	"strings"
)

// Config holds the fully resolved settings for a single run.
type Config struct {
	Branch string // positional argument: branch to review

	BaseRef string
	HeadRef string

	ObeyDoc  []string
	Template string

	Agent       string
	AgentConfig string

	ContextLines     int
	IncludeFullFiles bool

	MaxDiffBytes    int
	MaxDocBytes     int
	MaxFileBytes    int
	SnippetMaxChars int

	Out string

	// New in v2.
	AgentTimeout int  // seconds; 0 means no timeout
	NoProgress   bool // force plain (non-spinner) output
	StrictFetch  bool // treat a failed base fetch as fatal
	DryRun       bool // build the prompt but do not run the agent
	Lang         string
	Format       string // output format: text | json (json = NDJSON event stream)

	// Meta (not persisted).
	ConfigPath string
	NoConfig   bool
}

// Defaults returns the built-in default configuration.
func Defaults() Config {
	return Config{
		BaseRef:         "origin/main",
		HeadRef:         "HEAD",
		Agent:           "codex",
		ContextLines:    20,
		MaxDiffBytes:    600000,
		MaxDocBytes:     200000,
		MaxFileBytes:    200000,
		SnippetMaxChars: 25000,
		AgentTimeout:    600,
		Lang:            "en",
		Format:          "text",
	}
}

// fileConfig mirrors the JSON config file. Pointer fields distinguish "absent"
// from "zero value". Kebab-case keys keep backward compatibility with legacy.
type fileConfig struct {
	ObeyDoc          json.RawMessage `json:"obey-doc"` // string or []string
	Template         *string         `json:"template"`
	IncludeFullFiles *bool           `json:"include-full-files"`
	BaseRef          *string         `json:"base-ref"`
	HeadRef          *string         `json:"head-ref"`
	Agent            *string         `json:"agent"`
	AgentConfig      json.RawMessage `json:"agent-config"` // object or string
	ContextLines     *int            `json:"context-lines"`
	MaxDiffBytes     *int            `json:"max-diff-bytes"`
	MaxDocBytes      *int            `json:"max-doc-bytes"`
	MaxFileBytes     *int            `json:"max-file-bytes"`
	SnippetMaxChars  *int            `json:"snippet-max-chars"`
	Out              *string         `json:"out"`
	AgentTimeout     *int            `json:"agent-timeout"`
	NoProgress       *bool           `json:"no-progress"`
	StrictFetch      *bool           `json:"strict-fetch"`
	Lang             *string         `json:"lang"`
}

// obeyDocList decodes the obey-doc field, which may be a single string or an
// array of strings.
func (fc *fileConfig) obeyDocList() []string {
	if len(fc.ObeyDoc) == 0 {
		return nil
	}
	var arr []string
	if err := json.Unmarshal(fc.ObeyDoc, &arr); err == nil {
		return arr
	}
	var s string
	if err := json.Unmarshal(fc.ObeyDoc, &s); err == nil && s != "" {
		return []string{s}
	}
	return nil
}

// agentConfigString returns the agent-config as a JSON string. The file may
// store it either as an object (which we re-serialize) or as a string.
func (fc *fileConfig) agentConfigString() string {
	if len(fc.AgentConfig) == 0 {
		return ""
	}
	var s string
	if err := json.Unmarshal(fc.AgentConfig, &s); err == nil {
		return s
	}
	return string(fc.AgentConfig)
}

// applyFile overlays values from a parsed config file onto c.
func (c *Config) applyFile(fc *fileConfig) {
	if fc == nil {
		return
	}
	if v := fc.obeyDocList(); v != nil {
		c.ObeyDoc = append(c.ObeyDoc, v...)
	}
	if fc.Template != nil {
		c.Template = *fc.Template
	}
	if fc.IncludeFullFiles != nil {
		c.IncludeFullFiles = *fc.IncludeFullFiles
	}
	if fc.BaseRef != nil {
		c.BaseRef = *fc.BaseRef
	}
	if fc.HeadRef != nil {
		c.HeadRef = *fc.HeadRef
	}
	if fc.Agent != nil {
		c.Agent = *fc.Agent
	}
	if s := fc.agentConfigString(); s != "" {
		c.AgentConfig = s
	}
	if fc.ContextLines != nil {
		c.ContextLines = *fc.ContextLines
	}
	if fc.MaxDiffBytes != nil {
		c.MaxDiffBytes = *fc.MaxDiffBytes
	}
	if fc.MaxDocBytes != nil {
		c.MaxDocBytes = *fc.MaxDocBytes
	}
	if fc.MaxFileBytes != nil {
		c.MaxFileBytes = *fc.MaxFileBytes
	}
	if fc.SnippetMaxChars != nil {
		c.SnippetMaxChars = *fc.SnippetMaxChars
	}
	if fc.Out != nil {
		c.Out = *fc.Out
	}
	if fc.AgentTimeout != nil {
		c.AgentTimeout = *fc.AgentTimeout
	}
	if fc.NoProgress != nil {
		c.NoProgress = *fc.NoProgress
	}
	if fc.StrictFetch != nil {
		c.StrictFetch = *fc.StrictFetch
	}
	if fc.Lang != nil {
		c.Lang = *fc.Lang
	}
}

// applyEnv overlays values from CODEREV_* environment variables onto c.
func (c *Config) applyEnv(getenv func(string) string) {
	setStr := func(env string, dst *string) {
		if v := getenv(env); v != "" {
			*dst = v
		}
	}
	setInt := func(env string, dst *int) {
		if v := getenv(env); v != "" {
			if n, err := strconv.Atoi(v); err == nil {
				*dst = n
			}
		}
	}
	setBool := func(env string, dst *bool) {
		if v := getenv(env); v != "" {
			*dst = isTruthy(v)
		}
	}
	setStr("CODEREV_BASE_REF", &c.BaseRef)
	setStr("CODEREV_HEAD_REF", &c.HeadRef) // new: head-ref env (legacy lacked this)
	setStr("CODEREV_TEMPLATE", &c.Template)
	setStr("CODEREV_AGENT", &c.Agent)
	setStr("CODEREV_AGENT_CONFIG", &c.AgentConfig)
	setStr("CODEREV_OUT", &c.Out)
	setStr("CODEREV_LANG", &c.Lang)
	setStr("CODEREV_FORMAT", &c.Format)
	setInt("CODEREV_CONTEXT_LINES", &c.ContextLines)
	setInt("CODEREV_MAX_DIFF_BYTES", &c.MaxDiffBytes)
	setInt("CODEREV_MAX_DOC_BYTES", &c.MaxDocBytes)
	setInt("CODEREV_MAX_FILE_BYTES", &c.MaxFileBytes)
	setInt("CODEREV_SNIPPET_MAX_CHARS", &c.SnippetMaxChars)
	setInt("CODEREV_AGENT_TIMEOUT", &c.AgentTimeout)
	setBool("CODEREV_NO_PROGRESS", &c.NoProgress)
	setBool("CODEREV_STRICT_FETCH", &c.StrictFetch)
}

func isTruthy(v string) bool {
	switch strings.ToLower(strings.TrimSpace(v)) {
	case "1", "true", "yes", "y", "on":
		return true
	}
	return false
}

// stringList is a flag.Value that appends each occurrence (repeatable flag).
type stringList struct{ vals *[]string }

func (s stringList) String() string {
	if s.vals == nil {
		return ""
	}
	return strings.Join(*s.vals, ",")
}
func (s stringList) Set(v string) error {
	*s.vals = append(*s.vals, v)
	return nil
}
