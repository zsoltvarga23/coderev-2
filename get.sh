#!/usr/bin/env bash
# Download-and-install the coderev CLI from GitHub Releases — no Go, no .NET, no
# build tools. Per-user, no root. The opposite of install.sh (which builds from
# source); this fetches a pre-built, checksum-verified binary.
#
#   ./get.sh                 # latest
#   ./get.sh v1.2.0          # a specific tag
#   curl -fsSL https://raw.githubusercontent.com/zsoltvarga23/coderev-2/main/get.sh | bash
#
# Installs to ~/.local/bin/coderev. After this, `coderev update` keeps it current.
# For the desktop GUI, download the .AppImage from the same Releases page instead.

set -euo pipefail

REPO="${CODEREV_UPDATE_REPO:-zsoltvarga23/coderev-2}"
VERSION="${1:-latest}"
BIN_DIR="$HOME/.local/bin"
TARGET="$BIN_DIR/coderev"

case "$(uname -s)" in
  Darwin) OS="darwin" ;;
  Linux)  OS="linux" ;;
  *) echo "Unsupported OS: $(uname -s)"; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64) ARCH="amd64" ;;
  aarch64|arm64) ARCH="arm64" ;;
  *) echo "Unsupported arch: $(uname -m)"; exit 1 ;;
esac
ASSET="coderev-${OS}-${ARCH}"

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing required tool: $1"; exit 1; }; }
need curl

auth=()
[ -n "${GITHUB_TOKEN:-}" ] && auth=(-H "Authorization: Bearer $GITHUB_TOKEN")

if [ "$VERSION" = "latest" ]; then
  API="https://api.github.com/repos/$REPO/releases/latest"
else
  API="https://api.github.com/repos/$REPO/releases/tags/$VERSION"
fi

echo "==> Resolving release ($VERSION)"
json="$(curl -fsSL "${auth[@]}" -H 'Accept: application/vnd.github+json' "$API")"

# Minimal JSON scraping (no jq dependency): pull the download URLs by asset name.
url_for() { echo "$json" | grep -o "https://[^\"]*/$1" | head -n1; }
BIN_URL="$(url_for "$ASSET")"
SUM_URL="$(url_for "checksums.txt")"
[ -n "$BIN_URL" ] || { echo "Release has no asset '$ASSET'."; exit 1; }
[ -n "$SUM_URL" ] || { echo "Release has no checksums.txt (refusing unverified install)."; exit 1; }

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
echo "==> Downloading $ASSET"
curl -fsSL "${auth[@]}" -o "$tmp/$ASSET" "$BIN_URL"
curl -fsSL "${auth[@]}" -o "$tmp/checksums.txt" "$SUM_URL"

echo "==> Verifying checksum"
want="$(grep -E "[* ]$ASSET\$" "$tmp/checksums.txt" | awk '{print $1}' | head -n1)"
[ -n "$want" ] || { echo "checksums.txt has no entry for $ASSET."; exit 1; }
if command -v sha256sum >/dev/null 2>&1; then
  got="$(sha256sum "$tmp/$ASSET" | awk '{print $1}')"
else
  got="$(shasum -a 256 "$tmp/$ASSET" | awk '{print $1}')"  # macOS
fi
[ "$got" = "$want" ] || { echo "Checksum mismatch: expected $want, got $got"; exit 1; }
echo "    OK"

echo "==> Installing"
mkdir -p "$BIN_DIR"
install -m 0755 "$tmp/$ASSET" "$TARGET"
echo "    installed: $TARGET"

case ":$PATH:" in
  *":$BIN_DIR:"*) ;;
  *) echo "    NOTE: $BIN_DIR is not on PATH. Add to your shell profile:";
     echo "          export PATH=\"\$HOME/.local/bin:\$PATH\"" ;;
esac

echo ""
echo "==> Done"
echo "  coderev <branch>     (open a new shell if PATH just changed)"
echo "  Update later with:   coderev update"
