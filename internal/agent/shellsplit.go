package agent

import "strings"

// shellSplit splits a command string into arguments, honoring single and double
// quotes and backslash escapes. It is a pragmatic substitute for Python's
// shlex.split used by the legacy tool.
func shellSplit(s string) []string {
	var args []string
	var cur strings.Builder
	inSingle, inDouble, has := false, false, false

	flush := func() {
		if has {
			args = append(args, cur.String())
			cur.Reset()
			has = false
		}
	}

	for i := 0; i < len(s); i++ {
		c := s[i]
		switch {
		case inSingle:
			if c == '\'' {
				inSingle = false
			} else {
				cur.WriteByte(c)
				has = true
			}
		case inDouble:
			if c == '"' {
				inDouble = false
			} else if c == '\\' && i+1 < len(s) && (s[i+1] == '"' || s[i+1] == '\\') {
				i++
				cur.WriteByte(s[i])
				has = true
			} else {
				cur.WriteByte(c)
				has = true
			}
		case c == '\'':
			inSingle = true
			has = true
		case c == '"':
			inDouble = true
			has = true
		case c == '\\' && i+1 < len(s):
			i++
			cur.WriteByte(s[i])
			has = true
		case c == ' ' || c == '\t' || c == '\n':
			flush()
		default:
			cur.WriteByte(c)
			has = true
		}
	}
	flush()
	return args
}
