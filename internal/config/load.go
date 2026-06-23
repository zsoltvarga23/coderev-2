package config

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
)

// defaultConfigPaths returns candidate config file locations in search order.
func defaultConfigPaths(repo string) []string {
	cwd, _ := os.Getwd()
	var paths []string
	paths = append(paths, filepath.Join(cwd, ".coderev.json"))
	paths = append(paths, filepath.Join(cwd, "coderev.json"))
	if repo != "" && repo != cwd {
		paths = append(paths, filepath.Join(repo, ".coderev.json"))
		paths = append(paths, filepath.Join(repo, "coderev.json"))
	}
	if runtime.GOOS == "windows" {
		base := os.Getenv("APPDATA")
		if base == "" {
			base, _ = os.UserHomeDir()
		}
		paths = append(paths, filepath.Join(base, "coderev", "config.json"))
	} else {
		base := os.Getenv("XDG_CONFIG_HOME")
		if base == "" {
			home, _ := os.UserHomeDir()
			base = filepath.Join(home, ".config")
		}
		paths = append(paths, filepath.Join(base, "coderev", "config.json"))
	}
	return paths
}

// loadFile loads and parses a config file. It returns the parsed config, the
// path that was used (empty if none), and a non-fatal warning string if a file
// existed but could not be parsed.
//
// If explicitPath is set, only that file is consulted. Otherwise the default
// search order is used and the first existing file wins.
func loadFile(explicitPath string, noConfig bool, repo string) (fc *fileConfig, used string, warn string) {
	if noConfig {
		return nil, "", ""
	}
	if explicitPath != "" {
		p := explicitPath
		if !filepath.IsAbs(p) {
			base := repo
			if base == "" {
				base, _ = os.Getwd()
			}
			p = filepath.Join(base, p)
		}
		return parseFileIfExists(p)
	}
	for _, p := range defaultConfigPaths(repo) {
		if _, err := os.Stat(p); err == nil {
			parsed, usedPath, w := parseFileIfExists(p)
			return parsed, usedPath, w // first existing file wins (parsed or not)
		}
	}
	return nil, "", ""
}

func parseFileIfExists(p string) (*fileConfig, string, string) {
	data, err := os.ReadFile(p)
	if err != nil {
		if os.IsNotExist(err) {
			return nil, "", ""
		}
		return nil, "", fmt.Sprintf("could not read config %s: %v", p, err)
	}
	var fc fileConfig
	if err := json.Unmarshal(data, &fc); err != nil {
		return nil, "", fmt.Sprintf("could not parse config %s: %v", p, err)
	}
	return &fc, p, ""
}
