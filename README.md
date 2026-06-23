# coderev (Go)

AI-alapú Pull Request review parancssori eszköz. Egy git ág diffjéből
összeállít egy promptot, átadja egy konfigurálható AI-ügynöknek (Codex, Copilot
vagy bármilyen CLI), és **élő konzolos visszajelzés** mellett kiírja a review-t.

A `coderev-legacy/` Python eszköz Go nyelvű újraírása, a
[hibák javításával](docs/02-hibak-es-hianyossagok.md) és élő haladásjelzéssel.

## Telepítés / build

```bash
go build -o coderev ./cmd/coderev      # vagy coderev.exe Windowson
```

Egyetlen statikus bináris, futásidejű függőség nélkül (a `git` CLI szükséges).

## Használat

```bash
coderev <branch> [opciók]
```

Példák:

```bash
# feature/x review az origin/main ellen
coderev feature/x

# develop ellen, angol kimenettel, fájlba mentve
coderev feature/x --base-ref origin/develop --lang en --out review.md

# prompt megtekintése agent-futtatás nélkül
coderev feature/x --dry-run

# .coderev.json generálása a repo gyökerébe a megadott opciókkal
coderev init --agent copilot --base-ref origin/develop --out review.md
coderev init custom.json --lang en --force   # egyedi útvonal, meglévő felülírása

# egyedi ügynök
coderev feature/x --agent-config '{"cmd":["mycli","review","--in","{prompt_file}"],"mode":"file"}'

# strukturált NDJSON esemény-kimenet (a desktop GUI / automatizáció számára)
coderev feature/x --format json
```

A flagek a `<branch>` argumentum előtt és után is megadhatók.

## Mit csinál másképp a legacynél

| Legacy hiba | Megoldás itt |
|---|---|
| Destruktív `git checkout` | Nincs checkout; `git diff`/`git show` ref-alapon |
| Csendes fetch-hiba | Figyelmeztetés, `--strict-fetch`-csel végzetes |
| Bájtszintű diff-csonkolás a hunk-elemzés előtt | A hunk-elemzés a teljes diffből; csonkolás csak a promptnak |
| Kontextus a working tree-ből | Kontextus a `--head-ref`-ből (`git show`) |
| `arg`/`file` módban a prompt a stdin-re is ment | Stdin csak `stdin` módban |
| Nincs timeout / streaming / haladásjelzés | `--agent-timeout`, élő streaming, spinner + lépés-állapotok |

## Konzolos visszajelzés

A futás közben lépés-állapotok (✓/✗), spinner, eltelt idő és mennyiségi adatok
jelennek meg, majd az AI válasza **élőben streamel** a konzolra. Pipe/CI alatt
automatikusan animáció nélküli, sormintás kimenetre vált (vagy `--no-progress`).

## Konfiguráció

`.coderev.json` (a legacy formátummal kompatibilis), `CODEREV_*` környezeti
változók, és CLI flagek. Precedencia: **CLI > env > JSON > beépített default**.
Részletek: [docs/](docs/).

A konfigfájlt a `coderev init [útvonal] [opciók]` paranccsal lehet legenerálni a
repo gyökerébe. A megadott flagek bekerülnek a fájlba, a többi kulcs az
alapértelmezett értékét kapja. Meglévő fájlt csak `--force` ír felül. Az `init`
szándékosan **nem** olvas be létező konfigot vagy env-változót, hogy a kimenet
kiszámítható legyen.

## Fejlesztés

```bash
go test ./...      # minden csomag tesztje
go vet ./...
```

Csomagstruktúra: [docs/03-go-ujratervezes.md](docs/03-go-ujratervezes.md).
