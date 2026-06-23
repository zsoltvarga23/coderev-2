# Telepítés

Egyszerű, **per-user** (admin nélküli) telepítés belsős használatra. Telepíthető
külön a **CLI**, külön a **GUI**, vagy **mindkettő**. Nincs aláírás/cert — a GUI
első indításakor a Windows SmartScreen figyelmeztethet: *„További információ" →
„Futtatás mindenképp"*.

## Előfeltételek

| Mit telepítesz | Mi kell hozzá a fordításhoz |
|---|---|
| CLI | [Go](https://go.dev/dl/) |
| GUI | [.NET SDK](https://dotnet.microsoft.com/download) **és** Go (a motor binárist a GUI mellé csomagoljuk) |

Futtatáskor mindkettőhöz kell a `git` a PATH-on. Valódi AI-review-hoz egy agent
CLI is kell (codex/copilot/claude); a Claude-hoz a különálló Claude Code CLI
(`npm i -g @anthropic-ai/claude-code`). Dry-run módban nem kell agent.

> A self-contained GUI **nem igényel .NET runtime-ot a futtató gépen** — a publi-
> kált mappa átmásolható más (azonos platformú) gépre is, telepítés nélkül futtatható.

## Windows

A repó gyökeréből:

```powershell
./install.ps1                 # CLI + GUI
./install.ps1 -Component cli  # csak a parancssori eszköz
./install.ps1 -Component gui  # csak az asztali app
./install.ps1 -Uninstall      # eltávolítás
```

Mit csinál:
- **CLI** → `%LOCALAPPDATA%\coderev\bin\coderev.exe`, és felveszi a mappát a
  felhasználói **PATH**-ra (új terminál kell hozzá). Utána bárhol: `coderev <branch>`.
- **GUI** → `%LOCALAPPDATA%\coderev\gui\` (self-contained, egy exe), a motor
  binárist mellécsomagolva, és **Start menü parancsikonnal** („coderev").

Hasznos kapcsolók: `-Prefix <mappa>` (más telepítési hely), `-NoPath`,
`-NoShortcut`, `-Rid linux-x64` (kereszt-publikálás).

## Linux / macOS

```bash
chmod +x install.sh
./install.sh            # CLI + GUI
./install.sh cli        # csak CLI
./install.sh gui        # csak GUI
./install.sh --uninstall
```

- **CLI** → `~/.local/bin/coderev` (legyen a `~/.local/bin` a PATH-on).
- **GUI** → `~/.local/share/coderev/gui/` + `.desktop` bejegyzés (megjelenik az
  alkalmazás-menüben), a motor mellécsomagolva.

## Hogyan találja a GUI a motort

A telepítő a `coderev` motort **a GUI mappájába másolja**, így az automatikusan
megtalálja. A keresési sorrend: `CODEREV_BIN` környezeti változó → az app mellé
csomagolt bináris → `PATH`. Külön motorra mutatás: állítsd a `CODEREV_BIN`-t.

## Terjesztés telepítő nélkül (másik gépre)

Mivel a CLI statikus bináris, a GUI pedig self-contained, elég átmásolni:
- CLI: a `coderev(.exe)` fájl bárhova, ami a PATH-on van;
- GUI: a teljes `gui/` mappa (benne a mellékelt motorral) — dupla kattintás a
  `CodeRev.App(.exe)`-re.

Csomag előállítása terjesztéshez: `coderev-desktop/publish.ps1` (lásd a desktop
README-t), vagy `./install.ps1 -Prefix <staging> -NoPath -NoShortcut` és a
keletkező mappa zippelése.
