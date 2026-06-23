# coderev (legacy) – Működési áttekintés

> Ez a dokumentum a `coderev-legacy/` Python projekt működését írja le, hogy
> alapot adjon a Go nyelvű újraírásnak.

## 1. Mi ez?

A `coderev` egy **parancssori (CLI) eszköz automatikus, AI-alapú Pull Request
review-hoz**. A működés lényege:

1. A felhasználó megad egy git ágat.
2. Az eszköz kiszámítja a diffet egy bázis-referenciához képest (`origin/main`).
3. A diffből, a környező kódkontextusból és opcionális dokumentumokból
   összeállít egy **promptot**.
4. A promptot átadja egy konfigurálható **AI-ügynöknek** (Codex, Copilot vagy
   tetszőleges külső CLI).
5. Az ügynök válaszát (a review-t) kiírja a konzolra és/vagy fájlba.

**Futtatási előfeltétel:** git repó gyökeréből (vagy azon belül) kell indítani.

- Nyelv: Python `^3.9`
- Csomagolás: Poetry (`pyproject.toml`)
- Belépési pont: `coderev:main` (a teljes logika egyetlen fájlban:
  [`coderev/__init__.py`](../coderev-legacy/coderev/__init__.py))
- Külső függőség: **nincs** (csak a Python standard library)

## 2. Magas szintű adatfolyam

```
              ┌─────────────┐
   CLI args ─▶│  argparse   │
   env vars ─▶│  + config   │──▶ feloldott beállítások
   .json    ─▶│  feloldás   │
              └─────────────┘
                     │
                     ▼
   ┌──────────────────────────────────────────────────────┐
   │ ensure_repo_root()   → git rev-parse --show-toplevel  │
   │ fetch_base()         → git fetch --prune <remote>     │
   │ checkout_branch()    → git checkout <branch>          │
   │ compute_diff()       → git diff base...head           │
   │ list_changed_files() → git diff --name-only           │
   └──────────────────────────────────────────────────────┘
                     │
                     ▼
   ┌──────────────────────────────────────────────────────┐
   │ parse_changed_hunks()   → @@ hunk fejlécek elemzése   │
   │ build_context_snippets()→ kontextus a working tree-ből│
   │ read_docs()             → obey-doc fájlok beolvasása  │
   │ build_prompt()          → a teljes prompt összerakása │
   └──────────────────────────────────────────────────────┘
                     │
                     ▼
   ┌──────────────────────────────────────────────────────┐
   │ load_agent_spec()  → preset vagy egyedi agent config  │
   │ run_agent()        → subprocess (stdin|arg|file mód)  │
   └──────────────────────────────────────────────────────┘
                     │
                     ▼
            stdout  +  opcionális --out fájl
```

## 3. Beállítások feloldási rendje

Az eszköz **négy forrásból** olvashat beállítást. Precedencia (erősebb felülír):

```
parancssori argumentum  >  környezeti változó  >  konfig fájl  >  beépített default
```

### Konfig fájl keresési sorrendje
1. `--config` / `-c` által megadott fájl
2. `.coderev.json` vagy `coderev.json` az aktuális könyvtárban
3. ugyanezek a repó gyökerében
4. felhasználói config: `%APPDATA%\coderev\config.json` (Windows) vagy
   `~/.config/coderev/config.json` (Linux/macOS)

A `--no-config` kikapcsolja a konfig fájl betöltését.

## 4. CLI argumentumok / beállítások

| Argumentum | Default | Env változó | Konfig kulcs | Leírás |
|---|---|---|---|---|
| `branch` (pozícionális) | — | — | — | A review-zandó ág (checkout történik rá). |
| `--config`, `-c` | — | — | — | Konfig fájl útvonala. |
| `--no-config` | ki | — | — | Ne töltsön be konfig fájlt. |
| `--base-ref` | `origin/main` | `CODEREV_BASE_REF` | `base-ref` | Diff bázis. `origin/`/`upstream/` előtag esetén előbb fetch. |
| `--head-ref` | `HEAD` | — | `head-ref` | A diff feje. |
| `--obey-doc` | (üres) | — | `obey-doc` | Ismételhető. Kötelezően betartandó dokumentumok. |
| `--template` | (üres) | `CODEREV_TEMPLATE` | `template` | Kimeneti sablon. |
| `--agent` | `codex` | `CODEREV_AGENT` | `agent` | `codex` vagy `copilot`. |
| `--agent-config` | (üres) | `CODEREV_AGENT_CONFIG` | `agent-config` | Egyedi agent JSON (felülírja `--agent`). |
| `--context-lines` | `20` | `CODEREV_CONTEXT_LINES` | `context-lines` | Kontextus sorok a hunkok körül. |
| `--include-full-files` | ki | — | `include-full-files` | A módosult fájlok teljes tartalmát is beteszi. |
| `--max-diff-bytes` | `600000` | `CODEREV_MAX_DIFF_BYTES` | `max-diff-bytes` | Max diff méret. |
| `--max-doc-bytes` | `200000` | `CODEREV_MAX_DOC_BYTES` | `max-doc-bytes` | Max doc/sablon méret. |
| `--max-file-bytes` | `200000` | `CODEREV_MAX_FILE_BYTES` | `max-file-bytes` | Max fájlméret teljes-fájl módban. |
| `--snippet-max-chars` | `25000` | `CODEREV_SNIPPET_MAX_CHARS` | `snippet-max-chars` | Max kontextus-részlet hossz. |
| `--out` | (üres) | `CODEREV_OUT` | `out` | Kimeneti fájl. |

## 5. Az AI-ügynök (agent) rendszer

Az agent egy külső parancs, amelyet a `run_agent()` indít subprocessként. Egy
`AgentSpec` írja le:

```python
@dataclass(frozen=True)
class AgentSpec:
    name: str
    cmd: List[str]   # parancs + argumentumok
    mode: str        # stdin | arg | file
    cwd: str = "."   # repó gyökeréhez relatív
    env: Dict[str, str] | None = None
```

### Beépített presetek
| Név | Parancs | Mód |
|---|---|---|
| `codex` | `codex exec -` | `stdin` |
| `copilot` | `copilot --prompt-file {prompt_file}` | `file` |

### Átadási módok (`mode`)
- **`stdin`** – a prompt az ügynök szabványos bemenetére megy.
- **`arg`** – a `{prompt}` helyőrzőt a parancsban a teljes prompt szövege váltja fel.
- **`file`** – a prompt ideiglenes fájlba kerül, és a `{prompt_file}` helyőrzőt
  ennek útvonala váltja fel.

### Windows-specifikus indítás
A `run_agent()` Windowson kezeli a `.cmd`/`.bat` (ComSpec-en át) és `.ps1`
(PowerShell-en át) végrehajtást.

## 6. A prompt felépítése (`build_prompt`)

A generált prompt szekciói sorrendben:

1. `# Task: Automated PR Review` – fejléc, ág és bázis megjelölése.
2. `## Instructions` – általános review-utasítás.
3. `## Documentation to OBEY` – az `--obey-doc` fájlok (ha vannak).
4. `## Result Template` – a kimeneti sablon (ha van).
5. `## Changed Files` – a módosult fájlok listája.
6. `## Diff` – a nyers (esetleg csonkolt) diff.
7. `## Extra Context` – kontextus-részletek a változások körül.
8. `## Full Contents of Changed Files` – teljes fájlok (`--include-full-files`).
9. `## Output Requirements` – kimeneti elvárások.

## 7. Diff-kontextus kinyerés

- A `parse_changed_hunks()` reguláris kifejezéssel
  (`@@ -a,b +c,d @@`) gyűjti ki a hunk-fejléceket, fájlonként a `+++ b/...` sor
  alapján rendelve.
- A `build_context_snippets()` minden hunkhoz a **working tree-ből** olvassa ki a
  fájlt, és `context_lines` sorral a hunk körül kivág egy részletet. A változó
  sorokat `>>` prefix jelöli.

## 8. Kimenet

- A review mindig a **stdout**-ra kerül.
- Ha `--out` meg van adva, fájlba is íródik (relatív útvonal a repó gyökeréhez
  képest, a szülő könyvtárat létrehozza).
- Diagnosztikai / figyelmeztető üzenetek a **stderr**-re mennek.

## 9. Hibakezelés és kilépési kódok

- Nem git repó / sikertelen diff / ismeretlen agent / hiányzó parancs → `SystemExit(2)`.
- Az agent nem-nulla kilépési kódját az eszköz továbbadja (`SystemExit(returncode)`).
- Sikeres lefutás → `0`.
- A `fetch_base()` hibáit **csendben elnyeli** (lásd a hibalistát).
