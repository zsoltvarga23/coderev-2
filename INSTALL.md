# Telepítés

Két út van, **válaszd a felsőt, ha csak használni szeretnéd**:

1. **Release-ből (ajánlott)** — előre lefordított, ellenőrzött bináris. **Nem kell
   Go, .NET SDK vagy bármilyen fejlesztői csomag a gépedre.** Telepítés után az
   alkalmazás **magát frissíti** (gombnyomásra a GUI-ban vagy `coderev update`
   a CLI-nél).
2. **Forrásból** (lentebb, „Fejlesztői telepítés") — fordítás Go-val és .NET
   SDK-val. Csak fejlesztéshez.

Nincs aláírás/cert — a GUI első indításakor a Windows SmartScreen figyelmeztethet:
*„További információ" → „Futtatás mindenképp"*.

---

## 1. Telepítés release-ből (SDK nélkül)

A kiadások a **[GitHub Releases](https://github.com/zsoltvarga23/coderev-2/releases)**
oldalon vannak. Telepíthető külön a **GUI**, külön a **CLI**, vagy mindkettő.

### GUI (asztali alkalmazás)

| Platform | Mit tölts le | Mit csinálj |
|---|---|---|
| **Windows** | `CodeRev-win-Setup.exe` | Futtasd — telepít, parancsikont rak ki, elindítja. |
| **Linux** | a `*.AppImage` fájl | `chmod +x` után dupla kattintás / futtatás. |

A GUI a **Velopack** keretrendszerrel készül: a `⬆ Frissítés` gombbal bármikor
ellenőrzi és egy lépésben telepíti az újabb verziót, majd újraindul. A `coderev`
motor a csomag **része**, külön nem kell telepíteni.

### CLI (parancssori eszköz)

**A leggyorsabb** — letöltő szkript (nem fordít, csak letölti és PATH-ra teszi a
checksum-ellenőrzött binárist):

```powershell
# Windows (PowerShell)
irm https://raw.githubusercontent.com/zsoltvarga23/coderev-2/main/get.ps1 | iex
```

```bash
# Linux / macOS
curl -fsSL https://raw.githubusercontent.com/zsoltvarga23/coderev-2/main/get.sh | bash
```

Vagy **kézzel**: töltsd le a platformodhoz tartozó binárist a Releases oldalról
(`coderev-windows-amd64.exe` / `coderev-linux-amd64` / `coderev-darwin-arm64`),
nevezd át `coderev`(.exe)-re, és tedd egy PATH-on lévő mappába.

---

## 2. Frissítés

| Komponens | Hogyan |
|---|---|
| **GUI** | A `⬆ Frissítés` gomb az appban: ellenőriz, letölt, újraindul. (Velopack) |
| **CLI** | `coderev update` — letölti és ellenőrzi (SHA-256) az új verziót, és lecseréli magát. `coderev update --check` csak megnézi, van-e újabb. |

Mindkettő ugyanarról a GitHub Release-ről frissít, amelyikből a telepítés
készült. Más forrás (pl. fork): `CODEREV_UPDATE_URL` (GUI) / `CODEREV_UPDATE_REPO`
(CLI) környezeti változó.

---

## Hogyan találja a GUI a motort

Keresési sorrend: `CODEREV_BIN` környezeti változó → az app mellé csomagolt
bináris → `PATH`. A release-csomagban a motor mellé van csomagolva, így
automatikusan megtalálja.

Futtatáskor kell a `git` a PATH-on. Valódi AI-review-hoz egy agent CLI is kell
(codex/copilot/claude); a Claude-hoz a különálló Claude Code CLI
(`npm i -g @anthropic-ai/claude-code`). Dry-run módban nem kell agent.

---

## Fejlesztői telepítés (forrásból)

Ehhez **kell** a fordítóeszköz: a CLI-hez [Go](https://go.dev/dl/), a GUI-hoz a
[.NET SDK](https://dotnet.microsoft.com/download) **és** Go (a motort a GUI mellé
fordítja). Forrásból, per-user, admin nélkül telepít:

```powershell
# Windows
./install.ps1                 # CLI + GUI
./install.ps1 -Component cli  # csak a parancssori eszköz
./install.ps1 -Component gui  # csak az asztali app
./install.ps1 -Uninstall      # eltávolítás
```

```bash
# Linux / macOS
chmod +x install.sh
./install.sh            # CLI + GUI
./install.sh cli        # csak CLI
./install.sh gui        # csak GUI
./install.sh --uninstall
```

- **CLI** → `%LOCALAPPDATA%\coderev\bin\coderev.exe` (Win) / `~/.local/bin/coderev`
  (Linux/macOS), felveszi a mappát a felhasználói **PATH**-ra.
- **GUI** → `%LOCALAPPDATA%\coderev\gui\` (Win) / `~/.local/share/coderev/gui/`
  (self-contained, a motor mellécsomagolva), Start menü / `.desktop` bejegyzéssel.

> A forrásból telepített GUI **nem** kap Velopack auto-update-et (az csak a
> release-csomagban van). Fejlesztéshez ez a normális; éles használatra a
> release-telepítés ajánlott.

Hasznos `install.ps1` kapcsolók: `-Prefix <mappa>`, `-NoPath`, `-NoShortcut`,
`-Rid linux-x64` (kereszt-publikálás).

Kiadás készítése (karbantartóknak): lásd **[RELEASING.md](RELEASING.md)**.
