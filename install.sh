#!/usr/bin/env bash
# Per-user (no root, no certificate) installer for coderev on Linux/macOS.
# Installs the CLI engine, the desktop GUI, or both.
#
#   ./install.sh                 # both
#   ./install.sh cli             # command-line tool only
#   ./install.sh gui             # desktop app only
#   ./install.sh --uninstall     # remove everything
#
# CLI -> ~/.local/bin/coderev  (ensure ~/.local/bin is on PATH)
# GUI -> ~/.local/share/coderev/gui  (+ bundled engine), with a .desktop entry.

set -euo pipefail

COMPONENT="all"
UNINSTALL=0
case "${1:-}" in
  cli) COMPONENT="cli" ;;
  gui) COMPONENT="gui" ;;
  all|"") COMPONENT="all" ;;
  --uninstall) UNINSTALL=1 ;;
  *) echo "Usage: ./install.sh [cli|gui|all|--uninstall]"; exit 1 ;;
esac

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_DIR="$HOME/.local/bin"
GUI_DIR="$HOME/.local/share/coderev/gui"
DESKTOP_FILE="$HOME/.local/share/applications/coderev.desktop"

case "$(uname -s)" in
  Darwin) RID="osx-$(uname -m | sed 's/x86_64/x64/;s/arm64/arm64/')" ;;
  *)      RID="linux-$(uname -m | sed 's/x86_64/x64/;s/aarch64/arm64/')" ;;
esac

if [ "$UNINSTALL" = "1" ]; then
  echo "==> Uninstalling"
  rm -f "$BIN_DIR/coderev"
  rm -rf "$HOME/.local/share/coderev"
  rm -f "$DESKTOP_FILE"
  echo "Done."
  exit 0
fi

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing: $1 - $2"; exit 1; }; }

build_engine() { # $1 = output path
  need go "install: https://go.dev/dl/"
  local ver="dev"
  [ -f "$REPO_ROOT/VERSION" ] && ver="$(tr -d '[:space:]' < "$REPO_ROOT/VERSION")"
  ( cd "$REPO_ROOT" && go build -ldflags "-X main.version=$ver" -o "$1" ./cmd/coderev )
}

if [ "$COMPONENT" = "cli" ] || [ "$COMPONENT" = "all" ]; then
  echo "==> Building the CLI (Go)"
  mkdir -p "$BIN_DIR"
  build_engine "$BIN_DIR/coderev"
  echo "    installed: $BIN_DIR/coderev"
  case ":$PATH:" in
    *":$BIN_DIR:"*) ;;
    *) echo "    NOTE: $BIN_DIR is not on PATH. Add this to your shell profile:";
       echo "          export PATH=\"\$HOME/.local/bin:\$PATH\"" ;;
  esac
fi

if [ "$COMPONENT" = "gui" ] || [ "$COMPONENT" = "all" ]; then
  echo "==> Publishing the GUI (.NET, self-contained, $RID)"
  need dotnet "install: https://dotnet.microsoft.com/download"
  mkdir -p "$GUI_DIR"
  dotnet publish "$REPO_ROOT/coderev-desktop/src/CodeRev.App/CodeRev.App.csproj" \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$GUI_DIR" >/dev/null
  echo "==> Bundling the engine with the GUI"
  build_engine "$GUI_DIR/coderev"
  chmod +x "$GUI_DIR/CodeRev.App" "$GUI_DIR/coderev" 2>/dev/null || true
  cp "$REPO_ROOT/coderev-desktop/src/CodeRev.App/Assets/coderev.png" "$GUI_DIR/coderev.png" 2>/dev/null || true

  mkdir -p "$(dirname "$DESKTOP_FILE")"
  cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Type=Application
Name=coderev
Comment=AI-powered PR review
Exec=$GUI_DIR/CodeRev.App
Icon=$GUI_DIR/coderev.png
Terminal=false
Categories=Development;
EOF
  echo "    installed: $GUI_DIR/CodeRev.App (+ bundled coderev), and a .desktop entry"
fi

echo ""
echo "==> Done"
[ "$COMPONENT" != "gui" ] && echo "  CLI:  coderev <branch>   (open a new shell if PATH just changed)"
[ "$COMPONENT" != "cli" ] && echo "  GUI:  from the app menu 'coderev', or run: $GUI_DIR/CodeRev.App"
echo "  Uninstall:  ./install.sh --uninstall"
